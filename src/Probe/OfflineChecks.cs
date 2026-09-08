using StoneshardCompanion;
using System.IO.Compression;
using System.Security.Cryptography;

internal static class OfflineChecks
{
    private static int passed;
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    private static async Task Fails(Func<Task> action,string expected){try{await action();}catch(Exception e){Check(e.Message.Contains(expected),"refused: "+expected);return;}throw new Exception("Expected refusal: "+expected);}
    public static async Task Run(string root)
    {
        IntentChecks.Run();
        foreach(var (vw,vh) in new[]{(1920d,1080d),(1280d,720d),(800d,600d),(200d,100d)}){
            var rect=HudGeometry.Clamp(new(40,50,568,178),vw,vh);
            foreach(var corner in Enum.GetValues<HudCorner>())foreach(double dx in new[]{-3000d,-120,0,140,3000})foreach(double dy in new[]{-3000d,-100,0,200,3000}){
                var r=HudGeometry.Drag(rect,corner,dx,dy,vw,vh);
                if(r.X<0||r.Y<0||r.Width<=0||r.Height<=0||r.X+r.Width>vw+.001||r.Y+r.Height>vh+.001)throw new Exception($"Out of bounds: {corner} {r}");
                if(corner!=HudCorner.Move){bool left=corner is HudCorner.TopLeft or HudCorner.BottomLeft,top=corner is HudCorner.TopLeft or HudCorner.TopRight;
                    if(Math.Abs((left?r.X+r.Width:r.X)-(left?rect.X+rect.Width:rect.X))>.001||Math.Abs((top?r.Y+r.Height:r.Y)-(top?rect.Y+rect.Height:rect.Y))>.001)throw new Exception("Opposite resize anchor moved");}
            }
            Check(true,$"125 drag cases within {vw}x{vh}, corners preserve opposite anchor");
        }
        var fallback=HudGeometry.Clamp(new(double.NaN,double.PositiveInfinity,-100,double.NaN),1280,720);
        Check(double.IsFinite(fallback.X)&&fallback.Width>=HudGeometry.MinWidth&&fallback.Height<=720,"invalid stored geometry sanitized");
        var wide=HudGeometry.Grid(900,120,12);var tall=HudGeometry.Grid(220,600,12);
        Check(wide.Columns>tall.Columns&&wide.Rows<tall.Rows,"wide and tall layouts reflow");
        foreach(var (w,h) in new[]{(HudGeometry.MinWidth,HudGeometry.MinHeight),(650d,216d),(356d,700d),(1600d,200d)}){
            double availableWidth=w-20-HudGeometry.NavigationSize-HudGeometry.NavigationGap,availableHeight=h-20-22-26-18;
            var g=HudGeometry.Grid(availableWidth,availableHeight,12);
            Check(g.Columns*g.Rows>=12&&g.CellWidth>=26&&g.CellHeight>=26&&availableHeight>=HudGeometry.NavigationSize,$"fixed left compass and 12 actions fit {w}x{h}");
        }
        foreach(uint flags in new uint[]{0,2,4,8,16,32,2|4|16|32})Check(HudPolicy.Show(false,true,flags),$"HUD remains available in menu/hover/loading flags {flags}");
        foreach(uint flags in new uint[]{1,1|8,1|2|32})Check(!HudPolicy.Show(false,true,flags),$"inventory/character panel hides HUD flags {flags}");
        Check(HudPolicy.Show(false,false,1),"stale inventory flag cannot hide disconnected backup controls");
        Check(HudPolicy.Show(false,false,0),"no game or no bridge leaves backup HUD visible");
        Check(!HudPolicy.Show(true,false,0),"explicit fold still hides HUD without a game");
        Check(!HudPolicy.CanWalk(false,false,false,false,0)&&!HudPolicy.CanWalk(true,true,false,true,0),"no game and main menu disable walking while HUD stays visible");
        Check(!HudPolicy.CanWalk(true,true,true,false,0)&&!HudPolicy.CanWalk(true,false,true,true,0),"background and stale states cannot trigger journey");
        Check(HudPolicy.CanWalk(true,true,true,true,8)&&!HudPolicy.CanWalk(true,true,true,true,2),"hover allows journey, native dialogs block it");
        var packet=new byte[3968];
        BitConverter.GetBytes(1).CopyTo(packet,12);BitConverter.GetBytes(1).CopyTo(packet,3772);
        BitConverter.GetBytes(Environment.TickCount64).CopyTo(packet,3872);
        BitConverter.GetBytes(64u).CopyTo(packet,68);BitConverter.GetBytes(1u).CopyTo(packet,3916);
        BitConverter.GetBytes(4).CopyTo(packet,3920);BitConverter.GetBytes(2327d).CopyTo(packet,3928);BitConverter.GetBytes(1183d).CopyTo(packet,3936);
        BitConverter.GetBytes(12345ul).CopyTo(packet,3952);BitConverter.GetBytes(1u).CopyTo(packet,3960);
        var decoded=EngineBridge.DecodeSnapshot(packet);
        Check(decoded.Ready&&decoded.Fresh&&decoded.SceneReady&&decoded.Capabilities==64&&decoded.WalkDirection==4&&decoded.WalkPhase==1&&decoded.WalkX==2327&&decoded.WalkY==1183&&decoded.LabelDrawOverrides==12345,"v5 snapshot decodes appended exit phase and preserves existing tail fields");
        await Fails(()=>Task.Run(()=>EngineBridge.DecodeSnapshot(new byte[3960])),"数据不完整");
        var p=new SupplyPolicy();var s=new SupplyObservation(10000,1,true,true,true,true,65,5,0,2,true);
        Check(p.Evaluate(s,true,false,25)==SupplyAction.None,"scene entry grace period");s=s with{Now=13000};
        Check(p.Evaluate(s with{Foreground=false},true,false,25)==SupplyAction.None,"background blocks automation");
        Check(p.Evaluate(s with{Safe=false},true,false,25)==SupplyAction.None,"combat or interaction blocks automation");
        Check(p.Evaluate(s with{Ready=false},true,false,25)==SupplyAction.None,"stale or blocked scene blocks automation");
        Check(p.Evaluate(s with{Thirst=double.NaN},true,false,25)==SupplyAction.None,"unknown thirst blocks drinking");
        Check(p.Evaluate(s with{WaterUses=0},true,false,25)==SupplyAction.None,"empty supplies never consume");
        Check(p.Evaluate(s,true,false,25)==SupplyAction.Drink,"high thirst schedules one drink");
        Check(p.Evaluate(s with{Now=90000},true,false,25)==SupplyAction.None,"in-flight action cannot repeat");
        p.Complete(SupplyAction.Drink,true,13000);
        Check(p.Evaluate(s with{Now=15000,Thirst=40,WaterUses=4},true,false,25)==SupplyAction.None,"confirmed action still has cooldown");
        Check(p.Evaluate(s with{Now=17000},true,false,25)==SupplyAction.None,"unchanged telemetry cannot repeat drink");
        Check(p.Evaluate(s with{Now=17000,Thirst=40,WaterUses=4},true,false,25)==SupplyAction.Drink,"verified relief above threshold can drink again");
        p.Complete(SupplyAction.Drink,false,17000);
        Check(p.Evaluate(s with{Now=30000,Scene=2},true,false,25)==SupplyAction.None,"failure stays latched across map changes");
        p.Evaluate(s with{Now=33000},false,false,25);
        Check(p.Evaluate(s with{Now=37000},true,false,25)==SupplyAction.Drink,"explicit re-enable permits retry");
        p.Reset();s=s with{Now=10000};p.Evaluate(s,false,true,25);
        Check(p.Evaluate(s with{Now=13000},false,true,25)==SupplyAction.TorchOn,"auto torch requests light");p.Complete(SupplyAction.TorchOn,true,13000);
        Check(p.Evaluate(s with{Now=18000,TorchState=1},false,true,25)==SupplyAction.None,"lit torch does not toggle repeatedly");
        Check(p.Evaluate(s with{Now=20000,TorchCount=0},false,true,25)==SupplyAction.None,"no spare torch produces no action");
        p.ManualAction(20000);Check(p.Evaluate(s with{Now=22000},false,true,25)==SupplyAction.None,"manual action delays automation");
        Check(p.Evaluate(s with{Now=24000},false,true,25)==SupplyAction.TorchOn,"exhausted torch can use spare after cooldown");
        await SaveChecks(root);
        Console.WriteLine($"{passed} v0.3 offline checks passed (plus prior speed-intent checks). No game/UI access.");
    }
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static async Task SaveChecks(string root)
    {
        string temp=Path.Combine(Path.GetTempPath(),"StoneshardCompanion-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try{
            string saves=Path.Combine(temp,"local","StoneShard"),backups=Path.Combine(temp,"backups"),data=Path.Combine(saves,"characters_v1","character_3","exitsave_1","data.sav");
            Directory.CreateDirectory(Path.GetDirectoryName(data)!);File.WriteAllText(data,"ALIVE_01");File.WriteAllText(Path.Combine(saves,"characters_v1","characters.map"),"CHARACTER_3");File.WriteAllText(Path.Combine(saves,"settings.ini"),"settings");
            string hidden=Path.Combine(saves,"hidden.sav");File.WriteAllText(hidden,"hidden");File.SetAttributes(hidden,FileAttributes.Hidden);
            var service=new SaveManagerService(Path.Combine(root,"src","Overlay","Assets","SaveManager","Invoke.ps1"),saves,backups);
            var original=Directory.EnumerateFiles(saves,"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(saves,p),Hash);
            var first=await service.RunAsync(SaveOperation.Backup);string firstHash=Hash(first.Archive);
            Check(first.Files==4&&first.Hash==firstHash&&File.Exists(first.Archive+".sha256"),"GUI service creates complete verified archive including hidden files");
            using(var zip=ZipFile.OpenRead(first.Archive))Check(zip.Entries.Count(e=>!e.FullName.EndsWith('/'))==4,"archive has all four fixture files");
            var latest=await service.RunAsync(SaveOperation.Latest);
            Check(service.List().Count==3&&File.Exists(Path.Combine(backups,"Stoneshard-latest.zip"))&&Hash(first.Archive)==firstHash,"refresh latest preserves immutable history");
            using(var handle=new FileStream(Path.Combine(backups,".manager.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))await Fails(()=>service.RunAsync(SaveOperation.Backup),"另一个存档管理器");
            Check(!service.Busy,"service unlocks after worker failure");
            File.WriteAllText(data,"DEAD__01");string extra=Path.Combine(saves,"death-only.sav");File.WriteAllText(extra,"extra");
            var restored=await service.RunAsync(SaveOperation.Restore,first.Archive);
            Check(restored.Files==4&&original.All(p=>Hash(Path.Combine(saves,p.Key))==p.Value)&&!File.Exists(extra),"GUI restore matches original tree and removes obsolete files");
            Check(restored.SafetyArchive is not null&&File.Exists(restored.SafetyArchive)&&Hash(first.Archive)==firstHash,"restore preserves original backup and creates insurance");
            using(var zip=ZipFile.OpenRead(restored.SafetyArchive!))using(var reader=new StreamReader(zip.GetEntry("StoneShard/characters_v1/character_3/exitsave_1/data.sav")!.Open()))Check(reader.ReadToEnd()=="DEAD__01","insurance contains pre-restore state");
            Check(!service.List()[0].Safety,"insurance is not default restore selection");
            string corrupt=Path.Combine(backups,"Stoneshard-corrupt.zip");File.Copy(first.Archive,corrupt);File.WriteAllText(corrupt+".sha256",new string('0',64));
            await Fails(()=>service.RunAsync(SaveOperation.Restore,corrupt),"校验失败");Check(original.All(p=>Hash(Path.Combine(saves,p.Key))==p.Value),"corrupt archive leaves current saves intact");
            await Fails(()=>service.RunAsync(SaveOperation.Restore,Path.Combine(temp,"outside.zip")),"请选择现有备份");
            string traversal=Path.Combine(backups,"Stoneshard-traversal.zip");using(var z=ZipFile.Open(traversal,ZipArchiveMode.Create))z.CreateEntry("../escaped.sav");
            await Fails(()=>service.RunAsync(SaveOperation.Restore,traversal),"不安全的路径");
            using(var locked=new FileStream(data,FileMode.Open,FileAccess.Read,FileShare.Read))await Fails(()=>service.RunAsync(SaveOperation.Restore,first.Archive),"占用");
            Check(original.All(p=>Hash(Path.Combine(saves,p.Key))==p.Value),"locked file failure leaves save tree intact");
        }finally{
            string target=Path.GetFullPath(temp),parent=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if(Path.GetDirectoryName(target)!=parent||!Path.GetFileName(target).StartsWith("StoneshardCompanion-tests-"))throw new Exception("Unsafe test cleanup target");
            foreach(var file in Directory.EnumerateFiles(target,"*",SearchOption.AllDirectories))File.SetAttributes(file,FileAttributes.Normal);
            Directory.Delete(target,true);
        }
    }
}
