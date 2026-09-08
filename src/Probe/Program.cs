using StoneshardCompanion;
using System.Text.Json;

try{
    if(args.Length>0&&args[0]=="OfflineTest"){await OfflineChecks.Run(Path.GetFullPath(args.Length>1?args[1]:"."),args.Length>2?Path.GetFullPath(args[2]):null);return;}
    if(args.Length>0&&args[0]=="SelfTest"){IntentChecks.Run();return;}
    if(args.Length>1&&args[0]=="Ui"){
        int pid=int.Parse(args[1]);var windows=new List<object>();
        Native.EnumWindows((h,_)=>{Native.GetWindowThreadProcessId(h,out var owner);if(owner==pid){var title=new System.Text.StringBuilder(256);Native.GetWindowText(h,title,256);Native.GetWindowRect(h,out var r);windows.Add(new{window=h.ToInt64(),title=title.ToString(),visible=Native.IsWindowVisible(h),style=Native.GetWindowLongPtr(h,-20).ToInt64(),bounds=new[]{r.Left,r.Top,r.Right,r.Bottom}});}return true;},0);
        Console.WriteLine(JsonSerializer.Serialize(windows,new JsonSerializerOptions{WriteIndented=true}));return;
    }
    var sessions=GameSession.Discover();
    if(args.Length==0){Console.WriteLine(JsonSerializer.Serialize(sessions.Select(s=>new{s.Pid,s.Path,s.Version,s.Started,Window=s.Window.ToInt64()}),new JsonSerializerOptions{WriteIndented=true}));return;}
    var session=sessions.Single(s=>s.Pid==int.Parse(args[0]));
    if(args.Length>1&&args[1]=="Observe"){
        Console.WriteLine(JsonSerializer.Serialize(Observation.Read(session),new JsonSerializerOptions{WriteIndented=true}));return;
    }
    if(args.Length>1&&args[1]=="Watch"){
        string path=args.Length>2?args[2]:"artifacts/lifecycle.jsonl";
        await using var output=new StreamWriter(path,append:true){AutoFlush=true};
        for(int i=0;i<2400;i++){
            try{await output.WriteLineAsync(JsonSerializer.Serialize(Observation.Read(session)));}catch(Exception e){await output.WriteLineAsync(JsonSerializer.Serialize(new{error=e.Message}));break;}
            await Task.Delay(250);
        }
        return;
    }
    if(args.Length>1&&args[1]=="Status"){
        using var map=System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting($"Local\\StoneshardCompanion.v6.{session.Pid}",System.IO.MemoryMappedFiles.MemoryMappedFileRights.Read);
        using var view=map.CreateViewAccessor(0,4096,System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Read);
        Console.WriteLine(JsonSerializer.Serialize(new{sceneReady=view.ReadInt32(3772),ready=view.ReadInt32(12),preferred=view.ReadDouble(3792),suspend=view.ReadUInt32(3800),uiFlags=view.ReadUInt32(3804),updated=view.ReadInt64(3872),now=Environment.TickCount64,foreground=session.IsForeground,target=view.ReadDouble(80),cameraMode=view.ReadInt32(160),visor=view.ReadInt32(164),samples=view.ReadUInt64(176),autoCenter=view.ReadInt32(3768)==1,cameraX=view.ReadDouble(96),cameraY=view.ReadDouble(104),cameraWidth=view.ReadDouble(112),cameraHeight=view.ReadDouble(120),mapWidth=view.ReadDouble(128),mapHeight=view.ReadDouble(136),playerX=view.ReadDouble(144),playerY=view.ReadDouble(152)},new JsonSerializerOptions{WriteIndented=true}));return;
    }
    if(args.Length>2&&args[1]=="Desktop"){
        using var ui=System.Diagnostics.Process.GetProcessById(int.Parse(args[2]));var window=ui.MainWindowHandle;
        Native.GetWindowRect(session.Window,out var gameRect);Native.GetWindowRect(window,out var overlayRect);
        bool above=false;var previous=Native.GetWindow(session.Window,3);for(int n=0;n<512&&previous!=0;n++,previous=Native.GetWindow(previous,3))if(previous==window){above=true;break;}
        Console.WriteLine(JsonSerializer.Serialize(new{foreground=session.IsForeground,overlayTopmost=(Native.GetWindowLongPtr(window,-20).ToInt64()&8)!=0,overlayAboveGame=above,gameBounds=new[]{gameRect.Left,gameRect.Top,gameRect.Right,gameRect.Bottom},overlayBounds=new[]{overlayRect.Left,overlayRect.Top,overlayRect.Right,overlayRect.Bottom}},new JsonSerializerOptions{WriteIndented=true}));return;
    }
    var library=Path.GetFullPath(args.Length>3?args[3]:"artifacts/native/StoneshardBridge.dll");
    if(args.Length>1&&args[1]=="Verify"){await Verification.Run(session,library,args.Length>4?args[4]:"artifacts/verification-v031.json");return;}
    using var bridge=await EngineBridge.ConnectAsync(session,library);
    if(args.Length>1&&args[1]=="InspectAll"){
        var output=new System.Text.StringBuilder();
        foreach(bool globals in new[]{false,true})for(int page=0;page<240;page++){
            var snapshot=await bridge.SendAsync(EngineCommand.Inspect,globals?-page-1:page);
            output.AppendLine(snapshot.Diagnostic);
            var match=System.Text.RegularExpressions.Regex.Match(snapshot.Diagnostic,@"count=(\d+)");
            if(!match.Success||((page+1)*28>=int.Parse(match.Groups[1].Value)))break;
        }
        Directory.CreateDirectory("artifacts");File.WriteAllText("artifacts/live-variables.txt",output.ToString());
        Console.WriteLine("Saved artifacts/live-variables.txt");return;
    }
    var cmd=args.Length>1?Enum.Parse<EngineCommand>(args[1],true):EngineCommand.Refresh;
    double arg=args.Length>2?double.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture):0;
    var state=await bridge.SendAsync(cmd,arg);
    Console.WriteLine(JsonSerializer.Serialize(state,new JsonSerializerOptions{WriteIndented=true}));
    if(args.Length>4)await Task.Delay(int.Parse(args[4]));
}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}
