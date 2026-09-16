using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace StoneshardCompanion;
// Inert host; never starts the real coordinator or creates HWNDs.
public sealed class App:Application {public static string Version=>"0.3.24";public static bool UiTestMode=>true;public static bool TestWindows=>true;}
public sealed class MainWindow:Window {
 public UserPreferences Preferences {get;}=new();
 public CharacterTelemetry CharacterData {get;set;}=new();
 public SaveManagerService Saves {get;}=new(saveRoot:Path.Combine(Path.GetTempPath(),"Stoneshard-preview-absent","StoneShard"),backupRoot:Path.Combine(Path.GetTempPath(),"Stoneshard-preview-absent","Backups"));
 public string SaveStatus=>"离线预览 · 不操作游戏存档";
 public void Backup(bool latest){}
 public SaveArchive? ResolveQuickBackup(IReadOnlyList<SaveArchive> archives)=>QuickBackupSelection.Resolve(archives,Preferences.QuickRestoreArchive);
 public void ChangeBackupFolder(string folder){}
 public Task SaveAsync(SaveOperation operation,string archive)=>Task.CompletedTask;
 public void SavePreferences(){}
 public Task<string> CraftFodder()=>Task.FromResult("离屏预览，不调用游戏");
}
public static class FeaturePreview {
 [STAThread] public static void Main(string[] args){
  var app=new App();SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
  string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
  var owner=new MainWindow();
  owner.CharacterData=new(){At=Environment.TickCount64,Player=123,FoodsComplete=true,Foods=[new("o_inv_blueberry","蓝莓",3,7),new("o_inv_morel","羊肚菌",0,2)],Stats=StatsCatalog.All.Select((d,i)=>new StatReading(d.Key,i==0?15: i==10?112.5:10+i*.25,i==10?100:null)).ToArray()};
  var stats=new StatsWindow(owner);stats.Refresh(owner.CharacterData);Export(stats,"stats-tall.png",300,850);
  var buttons=Descendants((DependencyObject)stats.Content).OfType<Button>().ToArray();buttons.Single(b=>Equals(b.Content,"设为基线")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
  owner.CharacterData.Stats=owner.CharacterData.Stats.Select(s=>s with {Value=s.Key=="STR"?s.Value-2:s.Value+1.5}).ToArray();stats.Refresh(owner.CharacterData);
  Export(stats,"stats-wide.png",1000,390);Export(stats,"stats-small.png",250,250);
  if(Descendants((DependencyObject)stats.Content).OfType<ComboBox>().Any())throw new Exception("Unexpected category dropdown");
  if(Descendants((DependencyObject)stats.Content).OfType<Thumb>().Count(t=>t.ToolTip?.ToString()=="拖动此角调整宽高")!=4)throw new Exception("Missing resize corners");
  if(!Descendants((DependencyObject)stats.Content).OfType<TextBlock>().Any(t=>t.Text=="13（-2）"))throw new Exception("Baseline comparison not shown");
  var strengthText=Descendants((DependencyObject)stats.Content).OfType<TextBlock>().Single(t=>t.Text=="13（-2）");
  if(((SolidColorBrush)strengthText.Inlines.OfType<System.Windows.Documents.Run>().Last().Foreground).Color!=Color.FromRgb(255,132,135))throw new Exception("Worse baseline delta must be red");
  owner.CharacterData.Stats=owner.CharacterData.Stats.Select(s=>s.Key=="FMB"?s with {Value=s.Value-3}:s).ToArray();
  owner.CharacterData.Sources=[new("Weapon_Damage",10,1,"装备效果"),new("Weapon_Damage",-5,2,"受伤状态")];owner.CharacterData.SourcesAvailable=true;stats.Refresh(owner.CharacterData);
  var fmb=Descendants((DependencyObject)stats.Content).OfType<TextBlock>().Single(t=>t.Text.EndsWith("（-1.5 pp）"));
  if(((SolidColorBrush)fmb.Inlines.OfType<System.Windows.Documents.Run>().Last().Foreground).Color!=Color.FromRgb(112,220,155))throw new Exception("Lower fumble chance must be green");
  Export(stats,"stats-colored.png",1000,650);
  Export(new Window{Content=stats.BuildExplanation(StatsCatalog.All.Single(s=>s.Key=="Weapon_Damage"))},"stats-tooltip.png",390,460);
  var forage=new FodderWindow(owner);Export(forage,"forage-top.png",754,700);
  var groupRow=Descendants((DependencyObject)forage.Content).OfType<Grid>().Single(g=>g.Children.OfType<CheckBox>().Any(c=>c.ToolTip?.ToString()=="@edible_mushrooms"));
  if(groupRow.Children.OfType<TextBlock>().Single().Text!="2")throw new Exception("Shared mushroom count must sum edible inventory");
  var plus=Descendants(groupRow).OfType<RepeatButton>().Single(b=>Equals(b.Content,"▲"));plus.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
  if(owner.Preferences.ForageRules["@edible_mushrooms"].Keep!=1)throw new Exception("Quantity increment must save immediately");
  var minus=Descendants(groupRow).OfType<RepeatButton>().Single(b=>Equals(b.Content,"▼"));minus.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));minus.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
  if(owner.Preferences.ForageRules["@edible_mushrooms"].Keep!=0)throw new Exception("Quantity cannot go below zero");
  string configFixture=Path.Combine(output,"config-fixture-"+Guid.NewGuid().ToString("N"));
  string legacy=Path.Combine(configFixture,"old"),shared=Path.Combine(configFixture,"game","StoneshardCompanion");Directory.CreateDirectory(legacy);
  File.WriteAllText(Path.Combine(legacy,"preferences.json"),"original");GameConfiguration.MigratePreferences(legacy,shared);
  if(File.ReadAllText(Path.Combine(shared,"preferences.json"))!="original")throw new Exception("Config migration failed");
  File.WriteAllText(Path.Combine(legacy,"preferences.json"),"old-version-change");GameConfiguration.MigratePreferences(legacy,shared);
  if(File.ReadAllText(Path.Combine(shared,"preferences.json"))!="original")throw new Exception("Existing shared config must win");
  string settingsFixture=Path.Combine(configFixture,"range.json");
  foreach(int threshold in new[]{0,80,100}){
   File.WriteAllText(settingsFixture,"{\"ForageDefaultsVersion\":1,\"DurabilityWarning\":"+threshold+"}");
   if(UserPreferences.Read(settingsFixture).DurabilityWarning!=threshold)throw new Exception("Durability warning must preserve 0, 80 and 100 on reload");
  }
  File.WriteAllText(settingsFixture,"{\"ForageRules\":{\"o_inv_morel\":{\"Enabled\":true,\"Keep\":8}}}");
  if(UserPreferences.Read(settingsFixture).ForageRules["o_inv_morel"].Keep!=0||!File.Exists(settingsFixture+".before-zero-defaults"))throw new Exception("Initial reserve migration needs a backup and zero defaults");
  File.WriteAllText(settingsFixture,"{\"ForageDefaultsVersion\":1,\"ForageRules\":{\"o_inv_morel\":{\"Enabled\":true,\"Keep\":8}}}");
  if(UserPreferences.Read(settingsFixture).ForageRules["o_inv_morel"].Keep!=8)throw new Exception("Later custom reserve must survive restart");
  Console.WriteLine("Detected game directory: "+GameConfiguration.DiscoverDirectory());
  var scroll=Descendants((DependencyObject)forage.Content).OfType<ScrollViewer>().First();scroll.ScrollToVerticalOffset(520);Export(forage,"forage-middle.png",754,700);
  var save=new SaveWindow(owner);Export(save,"save-list.png",796,636);
  var list=Descendants((DependencyObject)save.Content).OfType<ListBox>().Single();
  list.ItemsSource=Enumerable.Range(1,30).Select(i=>new SaveArchive("preview-"+i,"Stoneshard-manual-2026-09-12_22-00-"+i+".zip",DateTime.Today.AddMinutes(i),8000000,false,false)).ToArray();list.SelectedIndex=0;
  Export(save,"save-list.png",796,636);
  var restore=Descendants((DependencyObject)save.Content).OfType<Button>().Single(b=>Equals(b.Content,"还原选中的备份"));restore.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
  Export(save,"save-confirm.png",796,636);
  var confirm=Descendants((DependencyObject)save.Content).OfType<Button>().Single(b=>Equals(b.Content,"确认还原此备份"));
  if(confirm.TranslatePoint(new Point(),(UIElement)save.Content).Y>=list.TranslatePoint(new Point(),(UIElement)save.Content).Y)throw new Exception("Restore confirmation must stay above scrolling list");
  list.SelectAll();Descendants((DependencyObject)save.Content).OfType<Button>().Single(b=>Equals(b.Content,"删除所选…")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
  Export(save,"save-delete-check.png",796,636);
  if(!save.Topmost||!forage.Topmost)throw new Exception("Configuration must remain topmost");
  Console.WriteLine("Offscreen previews and view checks passed. No windows, game process, input, settings or saves accessed.");
  void Export(Window window,string name,double w,double h){
   var root=(FrameworkElement)window.Content;root.Width=w;root.Height=h;root.Measure(new Size(w,h));root.Arrange(new Rect(0,0,w,h));root.UpdateLayout();
   var canvas=new DrawingVisual();using(var d=canvas.RenderOpen()){d.DrawRectangle(new SolidColorBrush(Color.FromRgb(25,23,31)),null,new Rect(0,0,w,h));d.DrawRectangle(new VisualBrush(root){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},null,new Rect(0,0,w,h));}
   var image=new RenderTargetBitmap((int)w,(int)h,96,96,PixelFormats.Pbgra32);image.Render(canvas);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var f=File.Create(Path.Combine(output,name));png.Save(f);
  }
 }
 private static IEnumerable<DependencyObject> Descendants(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in Descendants(VisualTreeHelper.GetChild(root,i)))yield return item;}
}
