using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace StoneshardCompanion;

public sealed class AppUpdater : IDisposable
{
    private readonly MainWindow owner;
    private readonly CancellationTokenSource cancellation=new();
    private readonly HttpClient http=new(){Timeout=TimeSpan.FromMinutes(10)};
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(5)};
    private string? package;
    private bool busy,installing,manual;
    private DateTime nextCheck=DateTime.MinValue;
    public string Status {get;private set;}="尚未检查更新";
    public bool Installing=>installing;
    public static string Root=>Path.Combine(UserPreferences.Folder,"Updates");
    public AppUpdater(MainWindow owner){this.owner=owner;timer.Tick+=async(_,_)=>{if(owner.Preferences.AutoUpdate&&DateTime.UtcNow>=nextCheck)await CheckAsync(false);await InstallWhenIdle();};}
    public void Start(){if(App.TestWindows)return;timer.Start();_=CheckAsync(false);}
    private void Report(string text){Status=text;owner.RefreshUpdateStatus();}
    public async Task CheckAsync(bool userRequested)
    {
        if(busy||installing||owner.ExitRequested||cancellation.IsCancellationRequested)return;
        if(!userRequested&&!owner.Preferences.AutoUpdate)return;
        if(package is not null){await InstallWhenIdle();return;}
        busy=true;manual=userRequested;nextCheck=DateTime.UtcNow.AddHours(6);
        try{
            Report("正在检查 GitHub 最新正式版…");
            var client=new ReleaseClient(http);
            using var checkTimeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);checkTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            var release=await client.CheckAsync(Version.Parse(App.Version),checkTimeout.Token);
            if(release is null){Report($"当前 v{App.Version} 已是最新版本");return;}
            string host=Environment.ProcessPath??"";
            if(!Path.GetFileName(host).Equals(UpdatePackage.Executable,StringComparison.OrdinalIgnoreCase)||!File.Exists(Path.Combine(AppContext.BaseDirectory,"SHA256.json"))){Report($"发现 v{release.Version}；开发运行环境请使用完整发行包更新");return;}
            Report($"发现 v{release.Version}，正在下载…");
            string work=Path.Combine(Root,Guid.NewGuid().ToString("N"));
            package=await client.DownloadAsync(release,work,new Progress<string>(Report),cancellation.Token);
            if(!Version.TryParse(FileVersionInfo.GetVersionInfo(Path.Combine(package,UpdatePackage.Executable)).FileVersion,out var binary)||new Version(binary.Major,binary.Minor,binary.Build)!=release.Version){package=null;throw new IOException("更新包内程序版本与 GitHub 版本不一致。");}
            Report($"v{release.Version} 已就绪；游戏关闭且存档操作完成后自动重启更新");
        }catch(OperationCanceledException){if(!cancellation.IsCancellationRequested)Report("检查更新超时，可稍后重试");}
        catch(Exception e){Report("更新未完成，当前版本继续可用："+e.Message);UserPreferences.Log("update "+e.Message);}
        finally{busy=false;}
        await InstallWhenIdle();
    }
    private async Task InstallWhenIdle()
    {
        if(package is null||busy||installing||owner.ExitRequested||cancellation.IsCancellationRequested||(!manual&&!owner.Preferences.AutoUpdate)||!owner.CanInstallUpdate)return;
        installing=true;
        try{
            Report("正在退出助手并安装更新…");
            string work=Path.GetDirectoryName(package)!;
            string helper=Path.Combine(work,"UpdateHelper.exe");File.Copy(Environment.ProcessPath!,helper,false);
            using var current=Process.GetCurrentProcess();
            var request=new UpdateRequest(current.Id,current.StartTime.ToFileTimeUtc(),AppContext.BaseDirectory,package);
            string task=Path.Combine(work,"request.json");File.WriteAllText(task,JsonSerializer.Serialize(request));
            var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=work};
            start.ArgumentList.Add("--apply-update");start.ArgumentList.Add(task);
            using var process=Process.Start(start)??throw new IOException("更新组件未能启动。");
            var watch=Stopwatch.StartNew();
            while(!File.Exists(Path.Combine(work,"ready"))){
                if(process.HasExited||watch.Elapsed>TimeSpan.FromSeconds(15))throw new IOException("更新组件未就绪，已保留当前版本。");
                await Task.Delay(100,cancellation.Token);
            }
            owner.RequestExit();
        }catch(Exception e){installing=false;Report("暂时无法安装更新："+e.Message);package=null;}
    }
    public void Dispose(){timer.Stop();cancellation.Cancel();http.Dispose();}
    public sealed record UpdateRequest(int Parent,long Started,string Target,string Package);
    public static int Apply(string requestPath)
    {
        string? target=null;bool parentExited=false;
        try{
            string task=Path.GetFullPath(requestPath),work=Path.GetDirectoryName(task)!;
            if(Path.GetDirectoryName(work)!=Path.GetFullPath(Root)||!Guid.TryParseExact(Path.GetFileName(work),"N",out _))throw new IOException("更新任务目录无效。");
            SavePaths.AssertNotLink(work);SavePaths.AssertNotLink(task);
            var request=JsonSerializer.Deserialize<UpdateRequest>(File.ReadAllText(task))??throw new IOException("更新任务无效。");
            if(Path.GetFullPath(request.Package)!=Path.Combine(work,"package"))throw new IOException("更新源目录无效。");
            target=SavePaths.ResolveDirectoryRoot(request.Target);
            UpdatePackage.Validate(request.Package);
            using(var parent=Process.GetProcessById(request.Parent)){
                if(parent.StartTime.ToFileTimeUtc()!=request.Started||!SavePaths.ResolveDirectoryRoot(Path.GetDirectoryName(parent.MainModule!.FileName)!).Equals(target,StringComparison.OrdinalIgnoreCase))throw new IOException("更新目标与运行中的助手不一致。");
                File.WriteAllText(Path.Combine(work,"ready"),"ready");
                if(!parent.WaitForExit(60000))throw new IOException("助手尚未退出，已取消更新。");
            }
            parentExited=true;
            var games=Process.GetProcessesByName("StoneShard");bool running=games.Length>0;foreach(var game in games)game.Dispose();
            if(running)throw new IOException("游戏正在运行，已取消更新。");
            UpdatePackage.Install(request.Package,target,Path.Combine(work,"rollback"));
            Restart(target);return 0;
        }catch(Exception e){
            UserPreferences.Log("update-install "+e);
            MessageBox.Show("自动更新未完成，旧文件和恢复材料已保留。\n"+e.Message,"晶石助手 · 更新",MessageBoxButton.OK,MessageBoxImage.Warning);
            if(parentExited&&target is not null&&e is not UpdateRollbackException){try{Restart(target);}catch(Exception restart){UserPreferences.Log("update-restart "+restart.Message);}}
            return 1;
        }
    }
    private static void Restart(string target)=>Process.Start(new ProcessStartInfo(Path.Combine(target,UpdatePackage.Executable)){UseShellExecute=false,WorkingDirectory=target});
}
