using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StoneshardCompanion;

public enum SaveOperation { Backup,Latest,Restore }
public sealed record SaveArchive(string Path,string Name,DateTime Modified,long Bytes,bool Safety,bool Latest)
{
    public bool Manual=>!Safety&&!Latest&&Name.StartsWith("Stoneshard-manual-",StringComparison.OrdinalIgnoreCase);
    public string Kind=>Safety?"操作前保险":Latest?"当前状态 latest · 可覆盖":Name.StartsWith("Stoneshard-current-state-",StringComparison.OrdinalIgnoreCase)?"当前状态快照":Name.StartsWith("Stoneshard-manual-",StringComparison.OrdinalIgnoreCase)?"手动保留备份":"旧版快照 · 来源未标注";
    public string Display=>$"[{Kind}] {Modified:MM-dd HH:mm:ss}  ·  {Bytes/1024.0:0.#} KB  ·  {Name}";
}
public static class QuickBackupSelection
{
    public static SaveArchive? Resolve(IEnumerable<SaveArchive> archives,string? rememberedPath)=>
        rememberedPath is null
            ?archives.Where(a=>a.Manual).OrderByDescending(a=>a.Modified).FirstOrDefault()
            :archives.FirstOrDefault(a=>a.Manual&&string.Equals(a.Path,rememberedPath,StringComparison.OrdinalIgnoreCase));
}
public sealed record SaveResult(string Archive,int Files,string Hash,string? SafetyArchive);

public sealed class SaveManagerService
{
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly string? workerPath;
    public string SaveRoot {get;}
    public string BackupRoot {get;}
    public bool Busy {get;private set;}
    public SaveManagerService(string? workerPath=null,string? saveRoot=null,string? backupRoot=null)
    {
        this.workerPath=workerPath is null?null:Path.GetFullPath(workerPath);
        SaveRoot=Path.GetFullPath(saveRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StoneShard"));
        BackupRoot=Path.GetFullPath(backupRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Stoneshard Save Backups"));
    }
    public IReadOnlyList<SaveArchive> List()
    {
        // Directory.Exists also returns false on denied/offline paths. Preserve
        // those errors so a disconnected share is not shown as "no backups".
        try{_ = File.GetAttributes(BackupRoot);}
        catch(DirectoryNotFoundException) when(!new Uri(BackupRoot).IsUnc){return [];}
        catch(FileNotFoundException) when(!new Uri(BackupRoot).IsUnc){return [];}
        string resolved=SavePaths.ResolveDirectoryRoot(BackupRoot);
        return new DirectoryInfo(resolved).EnumerateFiles("Stoneshard*.zip")
            .Where(f=>!SavePaths.IsLink(f.FullName))
            .Select(f=>new SaveArchive(f.FullName,f.Name,f.LastWriteTime,f.Length,f.Name.StartsWith("Stoneshard-before-",StringComparison.OrdinalIgnoreCase),f.Name.Equals("Stoneshard-latest.zip",StringComparison.OrdinalIgnoreCase)))
            .OrderBy(a=>a.Safety).ThenByDescending(a=>a.Modified).ToArray();
    }
    public Task<IReadOnlyList<SaveArchive>> ListAsync()=>Task.Run(List);
    public async Task<int> DeleteAsync(IReadOnlyList<SaveArchive> selected)
    {
        if(selected.Count==0)return 0;
        if(!await gate.WaitAsync(0))throw new InvalidOperationException("存档操作正在进行。");
        Busy=true;
        try{return await Task.Run(()=>{
            string root=SavePaths.ResolveDirectoryRoot(BackupRoot);
            if(new Uri(root).IsUnc)throw new IOException("网络备份目录不支持安全移入回收站，请在文件管理器中清理。");
            string lockPath=Path.Combine(root,".manager.lock");SavePaths.AssertNotLink(lockPath);
            using var guard=new FileStream(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            var available=List().ToDictionary(a=>a.Path,StringComparer.OrdinalIgnoreCase);
            var paths=selected.Select(a=>a.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach(string path in paths){
                if(!available.TryGetValue(path,out var current)||!Path.GetDirectoryName(Path.GetFullPath(path))!.Equals(root,StringComparison.OrdinalIgnoreCase))throw new IOException("备份列表已变化，请刷新后重新选择。");
                var original=selected.First(a=>a.Path.Equals(path,StringComparison.OrdinalIgnoreCase));
                if(current.Bytes!=original.Bytes||current.Modified!=original.Modified)throw new IOException("所选备份已改变，请重新核对。");
                SavePaths.AssertNotLink(path);SavePaths.AssertNotLink(path+".sha256");
            }
            int count=0;
            foreach(string path in paths){
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
                count++;
                if(File.Exists(path+".sha256"))Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path+".sha256",Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
            }
            return count;
        });}finally{Busy=false;gate.Release();}
    }
    public async Task<RespecResult> RespecAsync(RespecPreview preview)
    {
        if(!await gate.WaitAsync(0))throw new InvalidOperationException("存档操作正在进行。");
        Busy=true;
        try{return await Task.Run(()=>new SaveEngine(SaveRoot,BackupRoot).Respec(preview));}
        finally{Busy=false;gate.Release();}
    }
    public async Task<BuildEditResult> EditBuildAsync(BuildSnapshot snapshot,BuildAllocation draft)
    {
        if(!await gate.WaitAsync(0))throw new InvalidOperationException("存档操作正在进行。");
        Busy=true;
        try{return await Task.Run(()=>new SaveEngine(SaveRoot,BackupRoot).EditBuild(snapshot,draft));}
        finally{Busy=false;gate.Release();}
    }
    public async Task<LiveSaveResult> SaveNowAsync(Func<Task> trigger)
    {
        if(!await gate.WaitAsync(0))throw new InvalidOperationException("存档操作正在进行。");
        Busy=true;
        try{
            var safety=await Task.Run(()=>new SaveEngine(SaveRoot,BackupRoot).Run(SaveOperation.Backup));
            var before=await Task.Run(()=>LiveSave.Capture(SaveRoot));
            await trigger(); // At most once, even if native acknowledgement times out.
            var receipt=await LiveSave.WaitAsync(SaveRoot,before);
            try{
                var archive=await Task.Run(()=>new SaveEngine(SaveRoot,BackupRoot).Run(SaveOperation.Backup));
                return new(receipt,safety.Archive,archive.Archive,null);
            }catch(Exception e) when(e is IOException or UnauthorizedAccessException){return new(receipt,safety.Archive,null,e.Message);}
        }finally{Busy=false;gate.Release();}
    }
    public async Task<SaveResult> RunAsync(SaveOperation operation,string? archive=null,IProgress<string>? progress=null)
    {
        if(!await gate.WaitAsync(0))throw new InvalidOperationException("存档操作正在进行。");
        Busy=true;string? requestFile=null;
        try {
            // Backups only read saves. Run the existing engine on a background
            // thread so refreshing latest never relaunches the UI executable.
            if(operation is SaveOperation.Backup or SaveOperation.Latest)
                return await Task.Run(()=>new SaveEngine(SaveRoot,BackupRoot,message=>progress?.Report(message)).Run(operation));
            if(operation==SaveOperation.Restore){
                if(archive is null||!(await ListAsync()).Any(a=>string.Equals(a.Path,Path.GetFullPath(archive),StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("请选择现有备份目录中的存档。");
            }
            // ArgumentList and a JSON file keep file names and user paths out of shell code.
            requestFile=Path.Combine(Path.GetTempPath(),"StoneshardCompanion-"+Guid.NewGuid().ToString("N")+".json");
            await File.WriteAllTextAsync(requestFile,JsonSerializer.Serialize(new {Operation=operation.ToString(),SaveRoot,BackupRoot,Archive=archive}),new UTF8Encoding(false));
            var start=CreateWorkerStart();
            foreach(string arg in new[]{"--save-worker",requestFile})start.ArgumentList.Add(arg);
            using var process=new Process{StartInfo=start};process.Start();
            Task<string> errors=process.StandardError.ReadToEndAsync();
            string? result=null;
            while(await process.StandardOutput.ReadLineAsync() is {} line){
                if(line.StartsWith("@@RESULT@@",StringComparison.Ordinal))result=line[10..];
                else if(!string.IsNullOrWhiteSpace(line))progress?.Report(line);
            }
            // Never kill a restore mid-write. The UI waits and prevents exit until it finishes.
            await process.WaitForExitAsync();string error=await errors;
            if(result is null)throw new IOException($"存档组件未能启动或退出异常（{process.ExitCode}）。请将完整助手文件夹解压到 Windows 本地磁盘后重试。"+(error.Length>600?error[..600]:error));
            using var json=JsonDocument.Parse(result);var root=json.RootElement;
            if(!root.GetProperty("Ok").GetBoolean())throw new IOException(root.GetProperty("Error").GetString()??"存档操作失败。");
            if(process.ExitCode!=0)throw new IOException("存档组件退出异常；请查看备份目录。");
            var value=root.GetProperty("Result");
            return new(value.GetProperty("Archive").GetString()!,value.GetProperty("Files").GetInt32(),value.GetProperty("Hash").GetString()!,value.TryGetProperty("SafetyArchive",out var safety)?safety.GetString():null);
        } finally {
            Busy=false;gate.Release();
            if(requestFile is not null){try{File.Delete(requestFile);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
    private ProcessStartInfo CreateWorkerStart()
    {
        string host=workerPath??Environment.ProcessPath??throw new IOException("无法定位助手程序。");
        if(!File.Exists(host))throw new FileNotFoundException("缺少存档运行组件，请完整解压助手。",host);
        var start=new ProcessStartInfo(host){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        // Framework-dependent developer tests are launched via dotnet; shipped
        // builds always re-enter the exact self-contained application executable.
        if(workerPath is null&&Path.GetFileNameWithoutExtension(host).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,System.Reflection.Assembly.GetEntryAssembly()!.GetName().Name+".dll"));
        start.WorkingDirectory=Path.GetTempPath();
        return start;
    }
}
