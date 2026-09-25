using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using StoneshardCompanion;
class SaveUiProbe {
 [STAThread] static void Main(string[] args){
  if(args.Length==3&&args[2]=="--core"){
   string tmp=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.Combine(tmp,"StoneShard"));File.WriteAllText(Path.Combine(tmp,"StoneShard","fixture.sav"),"temporary");
   var service=new SaveManagerService(Path.GetFullPath(args[1]),Path.Combine(tmp,"StoneShard"),Path.Combine(tmp,"Backups"));
   for(int i=0;i<3;i++){File.AppendAllText(Path.Combine(tmp,"trace.txt"),"start "+i+"\n");var result=service.RunAsync(SaveOperation.Latest).GetAwaiter().GetResult();File.AppendAllText(Path.Combine(tmp,"trace.txt"),"ok "+result.Archive+"\n");}return;
  }
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};typeof(App).GetProperty("UiTestMode")!.SetValue(null,true);
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
  AppDomain.CurrentDomain.ProcessExit+=(_,_)=>File.AppendAllText(Path.Combine(root,"probe-status.txt"),"PROCESS EXIT "+Environment.ExitCode+" "+Environment.StackTrace+Environment.NewLine);
  app.Exit+=(_,_)=>File.AppendAllText(Path.Combine(root,"probe-status.txt"),"APP EXIT"+Environment.NewLine);
  string saves=Path.Combine(root,"StoneShard"),backups=Path.Combine(root,"Backups");Directory.CreateDirectory(saves);File.WriteAllText(Path.Combine(saves,"fixture.sav"),"temporary test save only");
  var owner=new MainWindow();typeof(MainWindow).GetProperty("Saves")!.SetValue(owner,new SaveManagerService(workerPath:Path.GetFullPath(args[1]),saveRoot:saves,backupRoot:backups));
  owner.Preferences.QuickRestoreArchive=null;
  var window=new SaveWindow(owner);typeof(MainWindow).GetField("savesWindow",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,window);
  var content=(FrameworkElement)window.Content;content.Measure(new Size(840,680));content.Arrange(new Rect(0,0,840,680));content.UpdateLayout();
  var button=Walk(content).OfType<Button>().Single(b=>Equals(b.Content,"更新当前状态（latest）"));
  Walk(content).OfType<Button>().Single(b=>Equals(b.Content,"保留一份手动备份")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
  PumpUntil(()=>owner.SaveStatus.StartsWith("备份成功")||owner.SaveStatus.StartsWith("存档操作失败"));
  string remembered=owner.Preferences.QuickRestoreArchive??throw new Exception("Manual backup target missing");
  if(!File.Exists(remembered)||UserPreferences.Load().QuickRestoreArchive!=remembered)throw new Exception("Manual backup target not persisted");
  for(int i=0;i<3;i++){
   File.AppendAllText(Path.Combine(root,"probe-status.txt"),"START "+i+Environment.NewLine);
   button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
   var deadline=DateTime.UtcNow.AddSeconds(30);
   while(!owner.SaveStatus.StartsWith("备份成功")&&!owner.SaveStatus.StartsWith("存档操作失败")&&DateTime.UtcNow<deadline){
    var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
   }
   File.AppendAllText(Path.Combine(root,"probe-status.txt"),owner.SaveStatus+Environment.NewLine);
   if(!owner.SaveStatus.StartsWith("备份成功")||!File.Exists(Path.Combine(backups,"Stoneshard-latest.zip")))throw new Exception(owner.SaveStatus);
   Console.WriteLine("PASS latest button round "+(i+1)+": "+owner.SaveStatus);
   if(owner.Preferences.QuickRestoreArchive!=remembered||UserPreferences.Load().QuickRestoreArchive!=remembered)throw new Exception("Latest replaced manual target");
  }
  var archives=owner.Saves.List();
  if(QuickBackupSelection.Resolve(archives,null)?.Path!=remembered)throw new Exception("Migration chose non-manual snapshot");
  if(QuickBackupSelection.Resolve(archives,remembered+"missing") is not null)throw new Exception("Missing target silently fell back");
  if(QuickBackupSelection.Resolve(archives.Where(a=>!a.Manual),null) is not null)throw new Exception("Migration selected automatic state");
  var second=owner.SaveAsync(SaveOperation.Backup);PumpUntil(()=>second.IsCompleted);second.GetAwaiter().GetResult();
  if(owner.Preferences.QuickRestoreArchive==remembered)throw new Exception("New manual backup did not advance target");
  remembered=owner.Preferences.QuickRestoreArchive??throw new Exception("New target missing");
  window.PrepareQuickRestore();
  PumpUntil(()=>Walk(content).OfType<TextBlock>().Any(t=>t.Text.StartsWith("将还原：")&&t.Text.Contains(Path.GetFileName(remembered))));
  var restore=Walk(content).OfType<Button>().Single(b=>Equals(b.Content,"确认还原此备份"));
  if(restore.IsEnabled)throw new Exception("Quick restore bypassed acknowledgement");
  if(File.ReadAllText(Path.Combine(saves,"fixture.sav"))!="temporary test save only")throw new Exception("Preparing restore modified saves");
  Console.WriteLine("PASS manual target persistence, latest isolation, migration, missing target, quick restore confirmation without save writes.");
  File.AppendAllText(Path.Combine(root,"probe-status.txt"),"ALL THREE PASSED"+Environment.NewLine);owner.RequestExit();Console.WriteLine("PASS real save window + backup engine, temporary paths only; no game connection or shown windows.");
 }
 static void PumpUntil(Func<bool> done){var deadline=DateTime.UtcNow.AddSeconds(30);while(!done()&&DateTime.UtcNow<deadline){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}if(!done())throw new TimeoutException("UI operation timed out");}
 static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var x in Walk(VisualTreeHelper.GetChild(root,i)))yield return x;}
}
