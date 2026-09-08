using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StoneshardCompanion;

public enum SaveOperation { Backup,Latest,Restore }
public sealed record SaveArchive(string Path,string Name,DateTime Modified,long Bytes,bool Safety,bool Latest)
{
    public string Display=>$"{(Safety?"[保险] ":Latest?"[最新副本] ":"")}{Modified:MM-dd HH:mm:ss}  ·  {Bytes/1024.0:0.#} KB  ·  {Name}";
}
public sealed record SaveResult(string Archive,int Files,string Hash,string? SafetyArchive);

public sealed class SaveManagerService
{
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly string runner;
    public string SaveRoot {get;}
    public string BackupRoot {get;}
    public bool Busy {get;private set;}
    public SaveManagerService(string runner,string? saveRoot=null,string? backupRoot=null)
    {
        this.runner=Path.GetFullPath(runner);
        SaveRoot=Path.GetFullPath(saveRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StoneShard"));
        BackupRoot=Path.GetFullPath(backupRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Stoneshard Save Backups"));
    }
    public IReadOnlyList<SaveArchive> List()
    {
        if(!Directory.Exists(BackupRoot))return [];
        return new DirectoryInfo(BackupRoot).EnumerateFiles("Stoneshard*.zip")
            .Where(f=>(f.Attributes&FileAttributes.ReparsePoint)==0)
            .Select(f=>new SaveArchive(f.FullName,f.Name,f.LastWriteTime,f.Length,f.Name.StartsWith("Stoneshard-before-restore-",StringComparison.OrdinalIgnoreCase),f.Name.Equals("Stoneshard-latest.zip",StringComparison.OrdinalIgnoreCase)))
            .OrderBy(a=>a.Safety).ThenByDescending(a=>a.Modified).ToArray();
    }
    public async Task<SaveResult> RunAsync(SaveOperation operation,string? archive=null,IProgress<string>? progress=null)
    {
        if(!await gate.WaitAsync(0))throw new InvalidOperationException("存档操作正在进行。");
        Busy=true;string? requestFile=null;
        try {
            if(!File.Exists(runner))throw new FileNotFoundException("缺少随附存档组件，请完整解压助手。",runner);
            if(operation==SaveOperation.Restore){
                if(archive is null||!List().Any(a=>string.Equals(a.Path,Path.GetFullPath(archive),StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("请选择现有备份目录中的存档。");
            }
            // ArgumentList and a JSON file keep file names and user paths out of shell code.
            requestFile=Path.Combine(Path.GetTempPath(),"StoneshardCompanion-"+Guid.NewGuid().ToString("N")+".json");
            await File.WriteAllTextAsync(requestFile,JsonSerializer.Serialize(new {Operation=operation.ToString(),SaveRoot,BackupRoot,Archive=archive}),new UTF8Encoding(false));
            var start=new ProcessStartInfo(FindPowerShell()){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
            foreach(string arg in new[]{"-NoLogo","-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",runner,"-RequestPath",requestFile})start.ArgumentList.Add(arg);
            using var process=new Process{StartInfo=start};process.Start();
            Task<string> errors=process.StandardError.ReadToEndAsync();
            string? result=null;
            while(await process.StandardOutput.ReadLineAsync() is {} line){
                if(line.StartsWith("@@RESULT@@",StringComparison.Ordinal))result=line[10..];
                else if(!string.IsNullOrWhiteSpace(line))progress?.Report(line);
            }
            // Never kill a restore mid-write. The UI waits and prevents exit until it finishes.
            await process.WaitForExitAsync();string error=await errors;
            if(result is null)throw new IOException("存档组件未返回结果。"+(error.Length>600?error[..600]:error));
            using var json=JsonDocument.Parse(result);var root=json.RootElement;
            if(!root.GetProperty("Ok").GetBoolean())throw new IOException(root.GetProperty("Error").GetString()??"存档操作失败。");
            if(process.ExitCode!=0)throw new IOException("存档组件退出异常；请查看备份目录。");
            var value=root.GetProperty("Result");
            return new(value.GetProperty("Archive").GetString()!,value.GetProperty("Files").GetInt32(),value.GetProperty("Hash").GetString()!,value.TryGetProperty("SafetyArchive",out var safety)?safety.GetString():null);
        } finally {
            Busy=false;gate.Release();
            if(requestFile is not null){try{File.Delete(requestFile);}catch(IOException){}}
        }
    }
    private string FindPowerShell()
    {
        // Resolve against the packaged worker, never the current directory or PATH.
        string portable=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(runner)!,"..","..","Runtime","PowerShell","pwsh.exe"));
        if(File.Exists(portable))return portable;
        if(Directory.Exists(Path.GetDirectoryName(portable)))throw new FileNotFoundException("随附存档运行组件不完整，请重新完整解压便携包。",portable);
        string standard=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PowerShell","7","pwsh.exe");
        if(File.Exists(standard))return standard;
        foreach(string folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)){
            if(string.IsNullOrWhiteSpace(folder))continue;
            string candidate=Path.Combine(folder.Trim('"'),"pwsh.exe");if(File.Exists(candidate))return candidate;
        }
        throw new FileNotFoundException("存档管理需要 PowerShell 7；请安装后重试。");
    }
}
