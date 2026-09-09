using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace StoneshardCompanion;

public sealed record ReleaseUpdate(Version Version,string Name,Uri Download,Uri Checksum,long Size);
public sealed class UpdateRollbackException(string message,Exception cause):IOException(message,cause);

public sealed class ReleaseClient(HttpClient client)
{
    public const string Repository="tidus2005/stoneshard-companion";
    public async Task<ReleaseUpdate?> CheckAsync(Version current,CancellationToken token=default)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,$"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("StoneshardCompanion/"+current.ToString(3));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version","2022-11-28");
        using var response=await client.SendAsync(request,token);response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(token),current);
    }
    public static ReleaseUpdate? Parse(string json,Version current)
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())return null;
        string tag=root.GetProperty("tag_name").GetString()??"";
        if(!Version.TryParse(tag.TrimStart('v'),out var version)||version.Build<0)throw new IOException("GitHub 版本号格式不受支持。");
        if(version<=new Version(current.Major,current.Minor,Math.Max(0,current.Build)))return null;
        string name=$"StoneshardCompanion-{version}-win-x64.zip";
        var assets=root.GetProperty("assets").EnumerateArray().ToArray();
        JsonElement Asset(string assetName){var found=assets.Where(a=>a.GetProperty("name").GetString()==assetName).ToArray();if(found.Length!=1)throw new IOException("发行版缺少唯一的 Windows x64 更新包或 SHA256 校验文件。");return found[0];}
        Uri Url(JsonElement asset,string assetName){
            string expected=$"https://github.com/{Repository}/releases/download/{tag}/{assetName}";
            if(asset.GetProperty("browser_download_url").GetString()!=expected)throw new IOException("更新文件不属于指定 GitHub 仓库。");
            return new Uri(expected);
        }
        var zip=Asset(name);var checksum=Asset(name+".sha256");long size=zip.GetProperty("size").GetInt64();
        if(size<1||size>512L*1024*1024)throw new IOException("更新包大小超出限制。");
        return new(version,name,Url(zip,name),Url(checksum,name+".sha256"),size);
    }
    public async Task<string> DownloadAsync(ReleaseUpdate release,string directory,IProgress<string>? progress=null,CancellationToken token=default)
    {
        Directory.CreateDirectory(directory);
        string checksum=await client.GetStringAsync(release.Checksum,token);
        string[] fields=checksum.Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        if(fields.Length!=2||fields[1].TrimStart('*')!=release.Name||fields[0].Length!=64||!fields[0].All(Uri.IsHexDigit))throw new IOException("更新校验文件格式错误。");
        string zip=Path.Combine(directory,"download.zip");
        using(var response=await client.GetAsync(release.Download,HttpCompletionOption.ResponseHeadersRead,token)){
            response.EnsureSuccessStatusCode();
            await using var input=await response.Content.ReadAsStreamAsync(token);
            await using var output=new FileStream(zip,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            byte[] buffer=new byte[81920];long total=0;int count,last=-1;
            while((count=await input.ReadAsync(buffer,token))>0){
                total+=count;if(total>release.Size)throw new IOException("更新下载长度不符。");
                await output.WriteAsync(buffer.AsMemory(0,count),token);
                int percent=(int)(total*100/release.Size);if(percent!=last){last=percent;progress?.Report($"下载 v{release.Version}：{percent}%");}
            }
            if(total!=release.Size)throw new IOException("更新包下载不完整。");
        }
        if(!UpdatePackage.Hash(zip).Equals(fields[0],StringComparison.OrdinalIgnoreCase))throw new IOException("更新包 SHA256 校验失败，保留当前版本。");
        return UpdatePackage.Extract(zip,Path.Combine(directory,"package"));
    }
}

public static class UpdatePackage
{
    public const string Executable="StoneshardCompanion.exe";
    private static readonly HashSet<string> Allowed=new(StringComparer.OrdinalIgnoreCase){Executable,"StoneshardBridge.dll","Start.cmd","LICENSE","THIRD_PARTY_NOTICES.md","使用说明.md","Assets/Icons/ATTRIBUTION.md"};
    public static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file));}
    private static string Name(string name)
    {
        name=name.Replace('\\','/');
        if(!Allowed.Contains(name))throw new IOException("更新包含未经允许的文件："+name);
        return name;
    }
    public static string Extract(string zipPath,string destination)
    {
        using var zip=ZipFile.OpenRead(zipPath);
        if(zip.Entries.Count>32)throw new IOException("更新包文件数量异常。");
        var files=zip.Entries.Where(e=>!e.FullName.EndsWith('/')&&!e.FullName.EndsWith('\\')).ToArray();
        string? folder=null;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
        foreach(var entry in files){
            string normalized=entry.FullName.Replace('\\','/');int split=normalized.IndexOf('/');
            if(split<1)throw new IOException("更新包缺少应用根目录。");
            string prefix=normalized[..split];folder??=prefix;
            if(prefix!=folder)throw new IOException("更新包包含多个根目录。");
            string name=normalized[(split+1)..];if(name!="SHA256.json")Name(name);
            if(!names.Add(name)||((entry.ExternalAttributes>>16)&0xF000)==0xA000||(entry.ExternalAttributes&(int)FileAttributes.ReparsePoint)!=0)throw new IOException("更新包包含重复文件或链接。");
            total+=entry.Length;if(total>1024L*1024*1024)throw new IOException("更新包解压大小超出限制。");
        }
        if(!names.SetEquals(Allowed.Append("SHA256.json")))throw new IOException("更新包文件不完整。");
        Directory.CreateDirectory(destination);
        foreach(var entry in files){
            string normalized=entry.FullName.Replace('\\','/');string target=Path.Combine(destination,normalized[(normalized.IndexOf('/')+1)..].Replace('/',Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);entry.ExtractToFile(target,false);
        }
        Validate(destination);return destination;
    }
    public static IReadOnlyList<string> Validate(string directory)
    {
        SavePaths.AssertNotLink(directory);
        string manifest=Path.Combine(directory,"SHA256.json");SavePaths.AssertNotLink(manifest);
        using var doc=JsonDocument.Parse(File.ReadAllText(manifest));var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in doc.RootElement.EnumerateObject()){
            string name=Name(entry.Name);if(!names.Add(name))throw new IOException("更新清单包含重复文件。");
            string path=Path.Combine(directory,name.Replace('/',Path.DirectorySeparatorChar));
            for(string? p=path;p is not null&&p!=directory;p=Path.GetDirectoryName(p))SavePaths.AssertNotLink(p);
            if(!Hash(path).Equals(entry.Value.GetString(),StringComparison.OrdinalIgnoreCase))throw new IOException("更新文件校验失败："+name);
        }
        if(!names.SetEquals(Allowed))throw new IOException("更新清单不完整。");
        return names.Append("SHA256.json").ToArray();
    }
    // All old files are copied before any replacement. New files are first
    // written beside the target so publication stays on the same filesystem.
    public static void Install(string package,string target,string rollback)
    {
        var names=Validate(package);target=SavePaths.ResolveDirectoryRoot(target);
        if(!File.Exists(Path.Combine(target,Executable)))throw new IOException("目标目录缺少原助手程序。");
        Directory.CreateDirectory(rollback);var existed=new HashSet<string>();var changed=new List<string>();
        foreach(string name in names){
            string path=Path.Combine(target,name.Replace('/',Path.DirectorySeparatorChar));
            for(string? p=path;p is not null;p=Path.GetDirectoryName(p))SavePaths.AssertNotLink(p);
            if(File.Exists(path)){
                using var probe=OpenForUpdate(path);
                string old=Path.Combine(rollback,name);Directory.CreateDirectory(Path.GetDirectoryName(old)!);
                using var backup=new FileStream(old,FileMode.CreateNew);probe.CopyTo(backup);existed.Add(name);
            }
        }
        try{
            foreach(string name in names){Replace(Path.Combine(package,name),Path.Combine(target,name));changed.Add(name);}
            Validate(target);
        }catch(Exception failure){
            var errors=new List<Exception>();
            foreach(string name in changed.AsEnumerable().Reverse()){
                string path=Path.Combine(target,name);
                try{if(existed.Contains(name))Replace(Path.Combine(rollback,name),path);else File.Delete(path);}catch(Exception e){errors.Add(e);}
            }
            if(errors.Count>0)throw new UpdateRollbackException("更新回退未完成，请从恢复目录恢复旧文件："+rollback,new AggregateException(errors.Prepend(failure)));
            throw;
        }
    }
    private static void Replace(string source,string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);string temp=target+".update-"+Guid.NewGuid().ToString("N");
        try{File.Copy(source,temp);if(Hash(source)!=Hash(temp))throw new IOException("更新复制校验失败。");RetrySharingViolation(()=>{File.Move(temp,target,true);return true;});}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    private static FileStream OpenForUpdate(string path)=>RetrySharingViolation(()=>new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None));
    private static T RetrySharingViolation<T>(Func<T> action)
    {
        // Antivirus and VM shared-folder drivers may hold the image briefly
        // after process exit. Retry boundedly; never terminate another process.
        for(int attempt=0;;attempt++){
            try{return action();}
            catch(IOException e) when(attempt<40&&((e.HResult&0xffff) is 32 or 33)){Thread.Sleep(250);}
        }
    }
}
