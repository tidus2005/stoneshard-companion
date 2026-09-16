using System.IO;
using System.Linq;
using System.Windows;
namespace StoneshardCompanion;
public partial class App : Application
{
    // Makes the otherwise taskbar-hidden HUD discoverable by desktop UI tests.
    // No game behavior, automation policy or save paths change in this mode.
    public static bool UiTestMode {get;private set;}
    public static bool TestWindows {get;private set;}
    public static string Version=>System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)??"未知";
    private Mutex? mutex;
    private EventWaitHandle? activation;
    private readonly System.Windows.Threading.DispatcherTimer activationPoll=new(){Interval=TimeSpan.FromMilliseconds(200)};
    protected override void OnStartup(StartupEventArgs e)
    {
        // These small static panels do not need the game's GPU. Software
        // rendering also avoids blank WPF settings/save windows on this host.
        System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
        UiTestMode=e.Args.Contains("--ui-test");
        TestWindows=UiTestMode||e.Args.Contains("--live-test");
        string instanceName=UiTestMode?"Local\\StoneshardCompanion.Overlay.UiTest":"Local\\StoneshardCompanion.Overlay";
        activation=new EventWaitHandle(false,EventResetMode.AutoReset,instanceName+".Activate");
        mutex=new Mutex(true,instanceName,out bool created);
        if(!created){activation.Set();Shutdown();return;}
        activationPoll.Tick+=(_,_)=>{
            if(MainWindow is MainWindow window&&window.IsLoaded&&activation.WaitOne(0))window.OpenSettings();
        };
        activationPoll.Start();
        DispatcherUnhandledException+=(_,a)=>{
            a.Handled=true;UserPreferences.Log("dispatcher-error "+a.Exception);
            if(MainWindow is MainWindow w&&w.IsLoaded){try{w.ShowError(a.Exception.Message);}catch(Exception report){UserPreferences.Log("error-display-failed "+report);}}
            else {MessageBox.Show("助手启动失败："+a.Exception.Message,"晶石助手",MessageBoxButton.OK,MessageBoxImage.Error);Shutdown(1);}
        };
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e){activationPoll.Stop();activation?.Dispose();mutex?.Dispose();UserPreferences.Log("application-exit code="+e.ApplicationExitCode);base.OnExit(e);}
}
