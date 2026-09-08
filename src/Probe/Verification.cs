using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text.Json;
using System.Text.RegularExpressions;
using StoneshardCompanion;

static class Verification
{
    public static async Task Run(GameSession session,string library,string output)
    {
        var checks=new List<object>();
        void Check(bool okay,string name,object? evidence=null){checks.Add(new{name,passed=okay,evidence});Console.WriteLine($"{(okay?"PASS":"FAIL")} {name}");if(!okay)throw new Exception(name);}
        EngineBridge? bridge=null;
        try{
            bridge=await EngineBridge.ConnectAsync(session,library);
            var initial=await bridge.SendAsync(EngineCommand.Refresh);
            Check((initial.Capabilities&2)!=0,"Playable test map available");
            try{using var other=await EngineBridge.ConnectAsync(session,library);throw new Exception("Second controller accepted");}
            catch(IOException e) when(e.Message.Contains("另一个控制器")){Check(true,"Second controller excluded");}
            double Frame(string d){var match=Regex.Match(d,@"o_cameraController:.*?image_index=([0-9.]+)");return double.Parse(match.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);}
            for(int speed=1;speed<=4;speed++){
                var state=await bridge.SendAsync(EngineCommand.Speed,speed);
                Check(state.Error==0 && state.TargetSpeed==initial.BaseSpeed*speed,$"Engine target {speed}x",state.TargetSpeed);
                await Task.Delay(300);
                var first=await bridge.SendAsync(EngineCommand.Diagnostic);var watch=Stopwatch.StartNew();
                await Task.Delay(2000);
                var last=await bridge.SendAsync(EngineCommand.Diagnostic);watch.Stop();
                double rate=(Frame(last.Diagnostic)-Frame(first.Diagnostic))/watch.Elapsed.TotalSeconds;
                checks.Add(new{name=$"Observed game steps at {speed}x",rate,ratio=rate/initial.BaseSpeed});
                Console.WriteLine($"Observed {rate:F1} game steps/s");
            }
            var invalid=await bridge.SendAsync(EngineCommand.Speed,5);Check(invalid.Error==4&&invalid.Multiplier==4,"Invalid multiplier rejected without changing speed");
            var before=await bridge.SendAsync(EngineCommand.Speed,1);
            var centered=await bridge.SendAsync(EngineCommand.Center);
            await Task.Delay(700);centered=bridge.ReadState();
            Check(centered.Error==0&&centered.CameraMode==1&&Math.Abs(centered.CameraX+centered.CameraWidth/2-centered.MapWidth/2)<1&&Math.Abs(centered.CameraY+centered.CameraHeight/2-centered.MapHeight/2)<1,"Camera remains at geometric center",centered);
            Check(centered.PlayerX==before.PlayerX&&centered.PlayerY==before.PlayerY,"Centering does not move the player");
            var returned=await bridge.SendAsync(EngineCommand.Player);Check(returned.Error==0&&returned.CameraMode==0,"Return to player");
            Check(before.VisorState==0||before.VisorState==1,"Movable visor available");
            var visor=await bridge.SendAsync(EngineCommand.Visor);Check(visor.Error==0&&visor.VisorState==1-before.VisorState,"Visor changes to opposite state",visor.VisorState);
            await Task.Delay(650);
            visor=await bridge.SendAsync(EngineCommand.Visor);Check(visor.Error==0&&visor.VisorState==before.VisorState,"Visor toggles back",visor.VisorState);
            await bridge.SendAsync(EngineCommand.Speed,3);await bridge.SendAsync(EngineCommand.Center);
            await bridge.SendAsync(EngineCommand.HighlightSet,1);await Task.Delay(800);
            Check(bridge.ReadState().HighlightApplied,"Virtual item highlight applied before disconnect");
            using var map=MemoryMappedFile.OpenExisting($"Local\\StoneshardCompanion.v6.{session.Pid}",MemoryMappedFileRights.Read);
            using var view=map.CreateViewAccessor(0,4096,MemoryMappedFileAccess.Read);
            bridge.Dispose();bridge=null;await Task.Delay(2200);
            Check(view.ReadDouble(80)==initial.BaseSpeed&&view.ReadInt32(160)==0,"Watchdog restores speed and camera after controller disconnect",new{target=view.ReadDouble(80),cameraMode=view.ReadInt32(160)});
            Check(view.ReadInt32(3860)==0&&view.ReadInt32(3912)==0,"Watchdog releases item highlight after controller disconnect");
        }finally{
            if(bridge is not null){try{await bridge.SendAsync(EngineCommand.Reset);}finally{bridge.Dispose();}}
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{at=DateTimeOffset.Now,session.Pid,session.Version,checks},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
