using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

namespace StoneshardCompanion;

// Documentation-only host: links the real view source and resources, with an
// inert coordinator. No app startup, HWND, game discovery, input or personal data.
public sealed class App:Application { public static bool UiTestMode=>false; }
public sealed class MainWindow:Window
{
    public UserPreferences Preferences {get;}=new(){ShowLabels=true,AutoWalkKeys=true,AutoDrink=true,AutoTorch=true,RememberSpeed=true};
    public DemoSaves Saves {get;}=new();
    public int PreferredSpeed {get;set;}=2;
    public string Status=>"界面预览 · 演示配置，未连接游戏";
    public string SaveStatus=>"演示：备份成功 · 12 个文件校验通过";
    public string SupplyStatus=>"界面预览 · 角色数值为演示数据";
    public void ChooseSpeed(int value)=>PreferredSpeed=value;
    public void ToggleWalkKeys(){} public void ToggleLabels(){} public void ToggleFold(){}
    public void Walk(int direction){} public void RunAction(EngineCommand command){}
    public void OpenSettings(){} public void OpenSaves(){} public void Backup(bool latest){}
    public void Reset(){} public void ResetLayout(){} public void SavePreferences(){}
    public void ChangeBackupFolder(string folder){}
    public Task SaveAsync(SaveOperation operation,string? archive=null)=>Task.CompletedTask;
}
public sealed class DemoSaves
{
    public bool Busy=>false;
    public string SaveRoot=>@"C:\Users\Player\AppData\Local\StoneShard";
    public string BackupRoot=>@"D:\游戏备份\Stoneshard";
    public Task<IReadOnlyList<SaveArchive>> ListAsync()=>Task.FromResult<IReadOnlyList<SaveArchive>>(new[]{
        new SaveArchive("demo-1","Stoneshard-all-saves-2026-09-09_21-30.zip",new(2026,9,9,21,30,0),153600,false,false),
        new SaveArchive("demo-2","Stoneshard-latest.zip",new(2026,9,9,21,30,0),153600,false,true),
        new SaveArchive("demo-3","Stoneshard-all-saves-2026-09-09_20-15.zip",new(2026,9,9,20,15,0),145408,false,false),
        new SaveArchive("demo-4","Stoneshard-before-restore-2026-09-09_20-00.zip",new(2026,9,9,20,0,0),148480,true,false)
    });
}
public static class DocImages
{
    [STAThread]
    public static int Main(string[] args)
    {
        string project=Path.GetFullPath(args.Length>0?args[0]:".");
        string output=Path.Combine(project,"docs","images");Directory.CreateDirectory(output);
        RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
        var app=new App();
        var source=XDocument.Load(Path.Combine(project,"src","Overlay","App.xaml"));
        XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources=new XElement(ns+"ResourceDictionary",new XAttribute(XNamespace.Xmlns+"x","http://schemas.microsoft.com/winfx/2006/xaml"),source.Root!.Element(ns+"Application.Resources")!.Elements());
        app.Resources=(ResourceDictionary)XamlReader.Parse(resources.ToString());
        var owner=new MainWindow();
        var state=new EngineState(0,127,60,120,2,0,0,1920,1080,2600,2600,1300,1300,0,1,1,"","",false,true,1,1,2,0,0,18,12,0,0,1920,1080,15,1,1,4,Environment.TickCount64,true){TorchCount=2,WalkKeysEnabled=true,HighlightApplied=true};
        var hud=new HudWindow(owner);hud.Update(state,true);
        Export(hud,"hud-wide-v038.png",900,240);
        hud.Update(state with{UpdatedAt=Environment.TickCount64},true);
        Export(hud,"hud-compact-v038.png",420,360);
        var saves=new SaveWindow(owner);
        Export(saves,"save-manager-v038.png",980,560);
        var settings=new SettingsWindow(owner);
        Prepare(settings,640,215);
        var scroll=(ScrollViewer)settings.Content;
        var panel=(StackPanel)scroll.Content;
        var supply=panel.Children.OfType<TextBlock>().Single(t=>t.Text=="自动补给");
        scroll.ScrollToVerticalOffset(supply.TransformToAncestor(panel).Transform(new Point(0,0)).Y+panel.Margin.Top-8);
        Export(settings,"supplies-settings-v038.png",640,215);
        Console.WriteLine("Exported four real-view previews with demo data; no windows, game or personal files accessed.");
        return 0;

        void Prepare(Window window,double width,double height)
        {
            var root=(FrameworkElement)window.Content;
            root.Width=width;root.Height=height;
            root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.Render);
            root.UpdateLayout();
        }
        void Export(Window window,string name,double width,double height)
        {
            Prepare(window,width,height);
            var root=(FrameworkElement)window.Content;
            var visual=new DrawingVisual();
            using(var draw=visual.RenderOpen()){
                draw.DrawRectangle(new SolidColorBrush(Color.FromRgb(25,23,31)),null,new Rect(0,0,width,height));
                draw.DrawRectangle(new VisualBrush(root){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,width,height)},null,new Rect(0,0,width,height));
            }
            var bitmap=new RenderTargetBitmap((int)(width*2),(int)(height*2),192,192,PixelFormats.Pbgra32);bitmap.Render(visual);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,name));png.Save(file);
        }
    }
}
