using System.IO;
using System.Linq;
using System.Windows;
namespace StoneshardCompanion;
public partial class App : Application
{
    // Makes the otherwise taskbar-hidden HUD discoverable by desktop UI tests.
    // No game behavior, automation policy or save paths change in this mode.
    public static bool UiTestMode {get;private set;}
    public static string Version=>System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)??"未知";
    private Mutex? mutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        // These small static panels do not need the game's GPU. Software
        // rendering also avoids blank WPF settings/save windows on this host.
        System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
        UiTestMode=e.Args.Contains("--ui-test");
        mutex=new Mutex(true,UiTestMode?"Local\\StoneshardCompanion.Overlay.UiTest":"Local\\StoneshardCompanion.Overlay",out bool created);
        if(!created){Shutdown();return;}
        DispatcherUnhandledException+=(_,a)=>{
            var path=UserPreferences.Folder;Directory.CreateDirectory(path);
            File.AppendAllText(Path.Combine(path,"errors.log"),$"{DateTimeOffset.Now:o} {a.Exception}\n");
            if(MainWindow is MainWindow w)w.ShowError(a.Exception.Message);
            a.Handled=true;
        };
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e){mutex?.Dispose();base.OnExit(e);}
}
