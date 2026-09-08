using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace StoneshardCompanion;

// All operations use literal file-system paths and .NET APIs. No shell, module
// discovery, system installation, game process or current-directory dependency.
public sealed class SaveEngine
{
    private readonly string saves,backups;
    private readonly Action<string> report;
    private sealed record Entry(string Name,long Bytes,string Hash,long Written);
    private sealed record Snapshot(Entry[] Entries)
    {
        public string Hash=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',Entries.Select(e=>$"{e.Name}\t{e.Bytes}\t{e.Hash}")))));
        public bool Same(Snapshot? other,bool writes=false)=>other is not null&&Entries.Length==other.Entries.Length&&Entries.Zip(other.Entries).All(p=>p.First.Name==p.Second.Name&&p.First.Bytes==p.Second.Bytes&&p.First.Hash==p.Second.Hash&&(!writes||p.First.Written==p.Second.Written));
    }
    public SaveEngine(string saveRoot,string backupRoot,Action<string>? progress=null)
    {
        saves=Path.TrimEndingDirectorySeparator(Path.GetFullPath(saveRoot));
        backups=Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupRoot));
        report=progress??(_=>{});
        if(!Path.GetFileName(saves).Equals("StoneShard",StringComparison.OrdinalIgnoreCase))throw new IOException("存档目录名称必须为 StoneShard。");
        if(Within(backups,saves)||Within(saves,backups))throw new IOException("备份目录与存档目录不能互相包含。");
    }
    public SaveResult Run(SaveOperation operation,string? archive=null)
    {
        if(!Enum.IsDefined(operation))throw new IOException("不支持的存档操作。");
        AssertParents(saves);AssertParents(backups);
        Directory.CreateDirectory(backups);
        string lockPath=Path.Combine(backups,".manager.lock");AssertOrdinary(lockPath);
        FileStream manager;
        try{manager=new(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException e){throw new IOException("另一个存档管理器正在操作，或备份目录无法写入。请等待其完成并检查目录权限。",e);}
        using(manager){return operation==SaveOperation.Restore?Restore(archive??throw new IOException("请选择现有备份。")):Backup(operation==SaveOperation.Latest).Result;}
    }
    private static bool Within(string path,string root)=>path.Equals(root,StringComparison.OrdinalIgnoreCase)||path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    private static void AssertOrdinary(string path)
    {
        try{if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("路径不能是目录链接或文件链接："+path);}
        catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
    }
    private static void AssertParents(string path)
    {
        for(string? p=path;p is not null;p=Path.GetDirectoryName(p)){AssertOrdinary(p);}
    }
    private static IEnumerable<string> Files(string root)
    {
        AssertOrdinary(root);
        foreach(string path in Directory.EnumerateFileSystemEntries(root)){
            AssertOrdinary(path);
            if(Directory.Exists(path)){foreach(string file in Files(path))yield return file;}
            else yield return path;
        }
    }
    private static string Hash(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    private static Snapshot ReadTree(string root)
    {
        if(!Directory.Exists(root))throw new DirectoryNotFoundException("找不到存档目录："+root);
        return new(Files(root).Select(path=>{var f=new FileInfo(path);return new Entry(Path.GetRelativePath(root,path),f.Length,Hash(path),f.LastWriteTimeUtc.Ticks);}).OrderBy(e=>e.Name,StringComparer.Ordinal).ToArray());
    }
    private static void CopyTree(string source,string destination)
    {
        Directory.CreateDirectory(destination);
        foreach(string path in Files(source)){
            string target=Path.Combine(destination,Path.GetRelativePath(source,path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(path,target,false);
        }
    }
    private static string NewWork(string parent,string prefix)
    {
        AssertParents(parent);Directory.CreateDirectory(parent);
        string path=Path.Combine(parent,prefix+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;
    }
    private void Cleanup(string work,string parent,string prefix)
    {
        // Only the exact work directory allocated by this operation is eligible.
        if(!Path.GetDirectoryName(work)!.Equals(parent,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(work).StartsWith(prefix,StringComparison.Ordinal))throw new IOException("拒绝清理未经验证的工作目录。");
        try{if(Directory.Exists(work)){AssertParents(work);DeleteTree(work);}}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){report("临时文件暂未清理，可稍后检查："+work);}
    }
    private static void DeleteTree(string root)
    {
        AssertOrdinary(root);
        foreach(string path in Directory.EnumerateFileSystemEntries(root)){
            AssertOrdinary(path);
            if(Directory.Exists(path))DeleteTree(path);
            else{File.SetAttributes(path,FileAttributes.Normal);File.Delete(path);}
        }
        Directory.Delete(root);
    }
    private static void Sidecar(string archive,string hash)=>File.WriteAllText(archive+".sha256",$"{hash}  {Path.GetFileName(archive)}{Environment.NewLine}",new UTF8Encoding(false));
    private (SaveResult Result,Snapshot Tree) Backup(bool latest=false,string prefix="Stoneshard-all-saves")
    {
        if(!Directory.Exists(saves))throw new DirectoryNotFoundException("找不到 Stoneshard 存档目录，请先在游戏中保存一次："+saves);
        string work=NewWork(backups,".work-");
        try{
            Snapshot? verified=null;string snapshot="";
            for(int attempt=1;attempt<=3;attempt++){
                report($"正在读取存档（第 {attempt} 次）...");snapshot=Path.Combine(work,"snapshot-"+attempt,"StoneShard");
                try{
                    var before=ReadTree(saves);CopyTree(saves,snapshot);var after=ReadTree(saves);var copy=ReadTree(snapshot);
                    if(before.Same(after,true)&&after.Same(copy)){verified=copy;break;}
                }catch(IOException) when(attempt<3){}
                report("检测到存档变化或文件被占用，稍后重试...");Thread.Sleep(200);
            }
            if(verified is null)throw new IOException("连续三次未能读取稳定的存档。请先回到主菜单或关闭游戏后再备份。");
            report("正在压缩并核对全部文件...");
            string zip=Path.Combine(work,"backup.zip");ZipFile.CreateFromDirectory(snapshot,zip,CompressionLevel.Optimal,true);
            string verify=Path.Combine(work,"verify");Extract(zip,verify);
            if(!verified.Same(ReadTree(Path.Combine(verify,"StoneShard"))))throw new IOException("压缩包解压校验失败，未发布备份。");
            string archive=Path.Combine(backups,$"{prefix}-{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}-{Guid.NewGuid():N}.zip");
            string hash=Hash(zip);Sidecar(zip,hash);
            // Publish the ZIP last: an interrupted commit may leave an orphan
            // checksum, but never a newly listed history ZIP without its hash.
            File.Move(zip+".sha256",archive+".sha256");File.Move(zip,archive);
            if(latest){
                string target=Path.Combine(backups,"Stoneshard-latest.zip");AssertOrdinary(target);AssertOrdinary(target+".sha256");
                string copy=Path.Combine(work,"latest.zip");File.Copy(archive,copy);
                if(Hash(copy)!=hash)throw new IOException("最新副本复制校验失败；历史备份已保留："+archive);
                Sidecar(copy,hash);File.Move(copy,target,true);File.Move(copy+".sha256",target+".sha256",true);
            }
            return (new(archive,verified.Entries.Length,hash,null),verified);
        }finally{Cleanup(work,backups,".work-");}
    }
    private static void Extract(string path,string destination)
    {
        using var zip=ZipFile.OpenRead(path);
        if(zip.Entries.Count==0||zip.Entries.Count>100000)throw new IOException("备份为空或文件数超出限制。");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
        // Validate every entry before creating any extracted file. Reject Windows
        // aliases, ADS, symlinks and case collisions even on a Mac-backed share.
        foreach(var entry in zip.Entries){
            string name=entry.FullName.Replace('\\','/').TrimEnd('/');
            string[] parts=name.Split('/');
            bool bad=parts.Any(p=>string.IsNullOrEmpty(p)||p is "." or ".."||p.EndsWith('.')||p.EndsWith(' ')||p.IndexOfAny(Path.GetInvalidFileNameChars())>=0||p.Contains(':')||IsDeviceName(p));
            if(bad||!names.Add(name)||((entry.ExternalAttributes>>16)&0xF000)==0xA000||(entry.ExternalAttributes&(int)FileAttributes.ReparsePoint)!=0)throw new IOException("压缩包包含不安全的路径或重复文件，已停止。");
            total=checked(total+entry.Length);if(total>16L*1024*1024*1024)throw new IOException("备份解压大小超过 16 GB，已停止。");
        }
        Directory.CreateDirectory(destination);
        foreach(var entry in zip.Entries){
            string name=entry.FullName.Replace('\\','/');string target=Path.GetFullPath(Path.Combine(destination,name.Replace('/',Path.DirectorySeparatorChar)));
            if(!Within(target,destination))throw new IOException("压缩包包含不安全的路径。");
            if(name.EndsWith('/')){Directory.CreateDirectory(target);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using(var input=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                byte[] buffer=new byte[81920];long written=0;int count;
                while((count=input.Read(buffer,0,buffer.Length))>0){written=checked(written+count);if(written>entry.Length)throw new IOException("压缩包文件长度校验失败。");output.Write(buffer,0,count);}
                if(written!=entry.Length)throw new IOException("压缩包文件长度校验失败。");
            }
            File.SetLastWriteTimeUtc(target,entry.LastWriteTime.UtcDateTime);
        }
    }
    private static bool IsDeviceName(string name)
    {
        string stem=name.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"||stem.Length==4&&(stem.StartsWith("COM")||stem.StartsWith("LPT"))&&"123456789¹²³".Contains(stem[3]);
    }
    private Snapshot? Quiet()
    {
        var first=Directory.Exists(saves)?ReadTree(saves):null;Thread.Sleep(1200);
        var second=Directory.Exists(saves)?ReadTree(saves):null;
        if((first is null)!=(second is null)||first is not null&&!first.Same(second,true))throw new IOException("游戏仍在写入存档。请停留在主菜单，稍等后重试。");
        return second;
    }
    private void Install(string staged,ref bool changed)
    {
        AssertParents(saves);var desired=ReadTree(staged);
        var existing=Directory.Exists(saves)?Files(saves).ToArray():[];
        foreach(var entry in desired.Entries){
            string target=Path.Combine(saves,entry.Name);AssertParents(target);
            if(Directory.Exists(target))throw new IOException("目标文件路径被目录占用："+entry.Name);
        }
        var probes=new List<FileStream>();
        try{foreach(string path in existing)probes.Add(new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read));}
        catch(IOException e){throw new IOException("存档文件被占用，当前存档未改动。",e);}
        finally{foreach(var probe in probes)probe.Dispose();}
        Directory.CreateDirectory(saves);
        foreach(var entry in desired.Entries.OrderBy(e=>Path.GetFileName(e.Name) switch{"characters.map"=>3,"character.map"=>2,"save.map"=>1,_=>0})){
            string target=Path.Combine(saves,entry.Name);
            if(File.Exists(target)&&Hash(target)==entry.Hash)continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);changed=true;
            File.Move(Path.Combine(staged,entry.Name),target,true);
        }
        var keep=desired.Entries.Select(e=>e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(string path in existing)if(!keep.Contains(Path.GetRelativePath(saves,path))){changed=true;File.Move(path,Path.Combine(Path.GetDirectoryName(staged)!,"removed-"+Guid.NewGuid().ToString("N")));}
    }
    private SaveResult Restore(string archive)
    {
        archive=Path.GetFullPath(archive);
        if(!Path.GetDirectoryName(archive)!.Equals(backups,StringComparison.OrdinalIgnoreCase)||!Path.GetExtension(archive).Equals(".zip",StringComparison.OrdinalIgnoreCase))throw new IOException("请选择备份目录内的 ZIP 存档。");
        AssertParents(archive);AssertOrdinary(archive+".sha256");
        using var guard=new FileStream(archive,FileMode.Open,FileAccess.Read,FileShare.Read);
        string archiveHash=Hash(archive);
        if(File.Exists(archive+".sha256")&&!string.Equals(File.ReadAllText(archive+".sha256").Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),archiveHash,StringComparison.OrdinalIgnoreCase))throw new IOException("备份校验失败，文件可能已损坏。");
        string parent=Path.GetDirectoryName(saves)!;string work=NewWork(parent,".StoneShard-restore-");
        bool retain=false;SaveResult? safety=null;
        try{
            report("正在解压并验证所选备份...");string extracted=Path.Combine(work,"extracted");Extract(archive,extracted);
            string source=Path.Combine(extracted,"StoneShard");if(!Directory.Exists(source))source=extracted;
            var expected=ReadTree(source);
            if(!expected.Entries.Any(e=>e.Name.EndsWith(".sav",StringComparison.OrdinalIgnoreCase))||!(Directory.Exists(Path.Combine(source,"characters_v1"))||Directory.Exists(Path.Combine(source,"characters"))))throw new IOException("备份不包含角色存档，已停止还原。");
            string staged=Path.Combine(work,"staged"),previous=Path.Combine(work,"previous");CopyTree(source,staged);
            if(!expected.Same(ReadTree(staged)))throw new IOException("待还原文件校验失败，当前存档未改动。");
            report("正在确认存档已停止写入，请保持游戏停留在菜单...");var quiet=Quiet();
            if(quiet is not null){
                report("正在备份还原前状态...");var backup=Backup(prefix:"Stoneshard-before-restore");safety=backup.Result;
                if(!quiet.Same(backup.Tree))throw new IOException("准备期间存档发生变化，已停止还原。");
            }
            var ready=Quiet();if((quiet is null)!=(ready is null)||quiet is not null&&!quiet.Same(ready,true))throw new IOException("存档在准备期间改变，当前存档未被替换。");
            if(ready is not null){CopyTree(saves,previous);if(!ready.Same(ReadTree(previous)))throw new IOException("回退副本校验失败，当前存档未改动。");}
            bool changed=false;report("正在替换存档文件...");
            try{Install(staged,ref changed);}
            catch(Exception e) when(e is IOException or UnauthorizedAccessException){
                retain=changed;
                if(changed&&ready is not null){try{bool rollback=false;Install(previous,ref rollback);retain=!ready.Same(ReadTree(saves));}catch(IOException){}catch(UnauthorizedAccessException){}}
                throw new IOException(retain?$"还原未完成；恢复材料保留在 {work}，保险备份：{safety?.Archive}。{e.Message}":"存档文件被占用或无法写入，本次还原已取消或回退。"+e.Message,e);
            }
            retain=true;
            if(!expected.Same(ReadTree(saves)))throw new IOException("替换后存档被改写；恢复材料保留在 "+work);
            Thread.Sleep(1200);
            if(!expected.Same(ReadTree(saves))||Hash(archive)!=archiveHash)throw new IOException("还原后文件又发生变化；恢复材料保留在 "+work);
            retain=false;return new(archive,expected.Entries.Length,expected.Hash,safety?.Archive);
        }finally{if(!retain)Cleanup(work,parent,".StoneShard-restore-");}
    }
}
