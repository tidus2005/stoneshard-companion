using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace StoneshardCompanion;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer poll=new(){Interval=TimeSpan.FromMilliseconds(250)};
    private readonly SpeedIntent intent=new();
    public UserPreferences Preferences {get;}=UserPreferences.Load();
    public int PreferredSpeed=>intent.Multiplier;
    public string Status {get;private set;}="等待游戏启动";
    private readonly HudWindow hud;
    private readonly SupplyPolicy supply=new();
    public SaveManagerService Saves {get;private set;}=null!;
    public string SaveStatus {get;private set;}="备份保存已落盘的进度";
    public bool ExitRequested {get;private set;}
    public AppUpdater Updater {get;}
    public bool CanInstallUpdate {
        get {
            if(closing||busy||Saves.Busy)return false;
            var games=Process.GetProcessesByName("StoneShard");bool idle=games.Length==0;foreach(var game in games)game.Dispose();return idle;
        }
    }
    public void RefreshUpdateStatus()=>settings?.RefreshUpdate(Updater.Status);
    public string SupplyStatus=>supply.Status;
    private SaveWindow? savesWindow;
    private readonly Forms.NotifyIcon tray;
    private EngineBridge? bridge;
    private GameSession? session;
    private SettingsWindow? settings;
    private nint hwnd;
    private bool busy,closing,closed,folded,polling,keysRegistered,resetPending,walkStarting;
    private long nextDiscovery,nextConnect;
    private int connectFailures;
    private string? unsupportedSession;
    private ulong lastScene,lastWindow;
    private readonly Dictionary<int,(uint Key,Action Action)> shortcuts=[];
    public MainWindow()
    {
        Saves=App.UiTestMode?new(saveRoot:Path.Combine(UserPreferences.Folder,"StoneShard"),backupRoot:Path.Combine(UserPreferences.Folder,"Backups")):new(backupRoot:Preferences.BackupFolder);
        InitializeComponent();hud=new(this);Updater=new(this);
        tray=new Forms.NotifyIcon{Text="晶石助手 · 行旅辅助",Icon=System.Drawing.SystemIcons.Application,Visible=true};
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("设置与快捷键",null,(_,_)=>Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("一键备份存档",null,(_,_)=>Dispatcher.Invoke(()=>Backup(false)));
        menu.Items.Add("存档管理",null,(_,_)=>Dispatcher.Invoke(OpenSaves));
        menu.Items.Add("显示 / 隐藏增强栏",null,(_,_)=>Dispatcher.Invoke(ToggleFold));
        menu.Items.Add("停止并恢复正常",null,(_,_)=>Dispatcher.Invoke(Reset));
        menu.Items.Add("退出助手",null,(_,_)=>Dispatcher.Invoke(RequestExit));tray.ContextMenuStrip=menu;
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(OpenSettings);
        SourceInitialized+=InitializeNative;Closing+=OnClosing;
        poll.Tick+=async(_,_)=>await PollAsync();
        Loaded+=async(_,_)=>{Hide();RefreshHud(null);poll.Start();Updater.Start();await PollAsync();};
    }
    private void InitializeNative(object? sender,EventArgs e)
    {
        hwnd=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(hwnd).AddHook(WndProc);
        for(int n=1;n<=4;n++){int target=n;shortcuts[n]=((uint)(0x30+n),()=>ChooseSpeed(target));}
        shortcuts[5]=(0x48,()=>RunAction(EngineCommand.Visor));
        shortcuts[6]=(0x43,()=>RunAction(EngineCommand.Center));shortcuts[7]=(0x52,()=>RunAction(EngineCommand.Player));
        shortcuts[8]=(0x4C,ToggleLabels);
        uint[] arrows=[0x26,0x28,0x25,0x27];for(int n=0;n<4;n++){int direction=n+1;shortcuts[12+n]=(arrows[n],()=>Walk(direction));}
        shortcuts[16]=(0x23,()=>Walk(0));
        shortcuts[17]=(0x24,()=>Walk(5));
        shortcuts[9]=(0x57,()=>RunAction(EngineCommand.Drink));shortcuts[10]=(0x54,()=>RunAction(EngineCommand.Torch));shortcuts[11]=(0x42,()=>Backup(false));
        if(App.UiTestMode)return;
        bool stopKey=Native.RegisterHotKey(hwnd,20,0x4003,0x53),foldKey=Native.RegisterHotKey(hwnd,21,0x4003,0x4F);
        if(!stopKey||!foldKey)ShowError("恢复或隐藏快捷键被占用，请从系统托盘操作。");
    }
    private nint WndProc(nint h,int msg,nint wp,nint lp,ref bool handled)
    {
        if(msg==0x312){handled=true;int id=wp.ToInt32();
            if(id==20)Reset();else if(id==21)ToggleFold();else if(session?.IsForeground==true&&shortcuts.TryGetValue(id,out var action))action.Action();
        }
        return 0;
    }
    private void RegisterKeys(bool enabled)
    {
        if(keysRegistered==enabled)return;
        foreach(var (id,binding) in shortcuts){if(enabled){if(!Native.RegisterHotKey(hwnd,id,0x4003,binding.Key))UserPreferences.Log($"hotkey-unavailable {id}");}else Native.UnregisterHotKey(hwnd,id);}
        keysRegistered=enabled;
    }
    private bool ProcessAlive()
    {
        if(session is null)return false;
        try{using var p=Process.GetProcessById(session.Pid);return !p.HasExited&&p.StartTime.ToFileTimeUtc()==session.Started;}catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}
    }
    private async Task PollAsync()
    {
        if(closing||polling)return;polling=true;EngineState? observed=null;
        try{
            long now=Environment.TickCount64;
            if(now>=nextDiscovery){
                nextDiscovery=now+1000;
                if(!ProcessAlive()){bridge?.Dispose();bridge=null;session=null;supply.Reset();}
                var games=App.UiTestMode?new List<GameSession>():GameSession.Discover();
                if(session is null){
                    var found=games.FirstOrDefault(g=>g.IsForeground)??(games.Count==1?games[0]:null);
                    if(found is not null){session=found;intent.BeginSession(found.Key,Preferences.RememberSpeed,Preferences.LastSpeed);connectFailures=0;nextConnect=0;unsupportedSession=null;resetPending=false;lastScene=lastWindow=0;UserPreferences.Log($"session {found.Key}");}
                }else{var current=games.FirstOrDefault(g=>g.Key==session.Key);if(current is not null)session=current;}
            }
            if(session is null){RegisterKeys(false);SetStatus("游戏未连接 · 可以备份或管理存档");return;}
            if(bridge is null&&!busy&&now>=nextConnect&&unsupportedSession!=session.Key){
                busy=true;SetStatus("正在校验并连接游戏");
                try{bridge=await EngineBridge.ConnectAsync(session,Path.Combine(AppContext.BaseDirectory,"StoneshardBridge.dll"));connectFailures=0;UserPreferences.Log("connected");}
                catch(Exception e){nextConnect=now+Math.Min(8000,1000*(1<<Math.Min(3,connectFailures++)));if(e is NotSupportedException)unsupportedSession=session.Key;SetStatus(e.Message);UserPreferences.Log("connect-failed "+e.Message);}
                finally{busy=false;}
            }
            if(closing){bridge?.Dispose();bridge=null;return;}
            if(bridge is null){RegisterKeys(false);return;}
            EngineState state;
            try{state=bridge.ReadState();observed=state;}catch(IOException){RegisterKeys(false);SetStatus("等待游戏更新状态 · 存档管理可用");return;}
            if(state.SceneGeneration!=lastScene||state.WindowGeneration!=lastWindow){UserPreferences.Log($"generation scene={state.SceneGeneration} window={state.WindowGeneration} selected={PreferredSpeed}");lastScene=state.SceneGeneration;lastWindow=state.WindowGeneration;}
            bool front=session.IsForeground,usable=state.Ready&&state.Fresh&&state.SceneReady&&state.UiFlags==0;
            RegisterKeys(front&&state.Ready&&state.Fresh&&state.SceneReady&&state.SpeedUiAllowed&&!busy);
            if(resetPending&&!busy&&state.Ready&&state.Fresh){if(await SendAsync(EngineCommand.Reset,0,false))resetPending=false;if(closing)return;state=bridge.ReadState();}
            if(front&&state.Ready&&state.Fresh&&state.SceneReady&&state.SpeedUiAllowed&&!busy&&intent.NeedsApply(state.PreferredMultiplier)){
                await SendAsync(EngineCommand.Speed,PreferredSpeed,false);if(closing)return;state=bridge.ReadState();
            }
            if(state.Ready&&state.Fresh&&!busy&&state.AutoCenter!=Preferences.AutoCenter)await SendAsync(EngineCommand.AutoCenter,Preferences.AutoCenter?1:0,false);
            if(state.Ready&&state.Fresh&&!busy&&(state.HighlightState==1)!=Preferences.ShowLabels)await SendAsync(EngineCommand.HighlightSet,Preferences.ShowLabels?1:0,false);
            if(closing)return;
            if(state.Ready&&state.Fresh&&!busy&&state.WalkKeysEnabled!=Preferences.AutoWalkKeys){await SendAsync(EngineCommand.WalkKeysSet,Preferences.AutoWalkKeys?1:0,false);if(closing)return;state=bridge.ReadState();}
            if(closing)return;
            if(!busy){
                var observation=new SupplyObservation(Environment.TickCount64,state.SceneGeneration,usable,front,state.WalkState!=1&&(state.SupplyFlags&2)!=0&&!Saves.Busy&&!hud.IsInteracting,(state.SupplyFlags&1)!=0,(state.VitalValid&2)!=0?state.Thirst:double.NaN,state.WaterUses,state.TorchState,state.TorchCount,true);
                var action=supply.Evaluate(observation,Preferences.AutoDrink,Preferences.AutoTorch,Preferences.DrinkThreshold);
                if(action!=SupplyAction.None){
                    bool ok=await SendAsync(action==SupplyAction.Drink?EngineCommand.Drink:EngineCommand.Torch,action==SupplyAction.Drink?1:action==SupplyAction.TorchOn?17:18,true);
                    supply.Complete(action,ok,Environment.TickCount64);if(closing)return;state=bridge.ReadState();
                }
            }
            SetStatus(!state.Ready?"等待游戏窗口恢复":!state.Fresh?"等待游戏响应":!state.SceneReady?"正在进入地图":!front?$"游戏在后台 · 已记住 {PreferredSpeed}×":!state.SpeedUiAllowed?$"原版菜单打开 · 已记住 {PreferredSpeed}×":$"已连接 · 所选 {PreferredSpeed}× / 当前 {state.Multiplier:0.##}×");
            observed=state;
        }catch(Exception e){if(!closing)ShowError(e.Message);}
        finally{polling=false;if(!closing)RefreshHud(observed);}
    }
    private void RefreshHud(EngineState? state)
    {
        if(!HudPolicy.Show(folded,state is {Ready:true,Fresh:true,SceneReady:true},state?.UiFlags??0)){HideHud();return;}
        hud.Update(state,session?.IsForeground==true);
        if(!hud.IsVisible)hud.Show();
        if(session is not null&&Native.IsWindow(session.Window)&&!Native.IsIconic(session.Window)&&Native.GetClientRect(session.Window,out var rect)&&rect.Right>0&&rect.Bottom>0){
            var point=new Native.Point();
            if(Native.ClientToScreen(session.Window,ref point)){
                rect.Left+=point.X;rect.Right+=point.X;rect.Top+=point.Y;rect.Bottom+=point.Y;hud.Place(rect,session.Window);return;
            }
        }
        hud.PlaceDesktop();
    }
    private void HideHud(){hud.Hide();}
    private void SetStatus(string status){Status=status;settings?.RefreshStatus(status);}
    public void ChooseSpeed(int multiplier)
    {
        intent.Select(multiplier);Preferences.LastSpeed=multiplier;SavePreferences();ReturnToGame();
        UserPreferences.Log($"speed-intent {multiplier} revision={intent.Revision}");
    }
    public void Reset()
    {
        intent.Stop();Preferences.LastSpeed=1;Preferences.AutoCenter=false;Preferences.ShowLabels=false;Preferences.AutoDrink=false;Preferences.AutoTorch=false;Preferences.AutoWalkKeys=false;supply.Reset();SavePreferences();resetPending=true;
        UserPreferences.Log("explicit-reset");SetStatus("已选择正常速度，正在恢复");
    }
    public void ToggleLabels(){Preferences.ShowLabels=!Preferences.ShowLabels;SavePreferences();ReturnToGame();}
    public void ToggleWalkKeys(){Preferences.AutoWalkKeys=!Preferences.AutoWalkKeys;SavePreferences();ReturnToGame();}
    private bool ReturnToGame()
    {
        if(session is null)return false;
        Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out uint pid);
        if(pid==Environment.ProcessId)Native.SetForegroundWindow(session.Window);
        return session.IsForeground;
    }
    public async void Walk(int direction)
    {
        if(walkStarting||closing)return;
        walkStarting=true;
        try{
            UserPreferences.Log($"walk-request direction={direction}");
            if(bridge is null||busy){UserPreferences.Log("walk-skipped controller unavailable or busy");return;}
            var requestedSession=session;
            if(!ReturnToGame())await Task.Delay(100);
            if(closing||session!=requestedSession||session?.IsForeground!=true){UserPreferences.Log("walk-skipped game did not regain foreground");return;}
            if(bridge is null||busy)return;
            var state=bridge.ReadState();if(!state.Ready||!state.Fresh)return;
            if(direction!=0&&(!state.SceneReady||(state.UiFlags&~8u)!=0))return;
            supply.ManualAction(Environment.TickCount64);await SendAsync(EngineCommand.Walk,direction,true);
        }catch(Exception e){if(!closing)ShowError(e.Message);}
        finally{walkStarting=false;}
    }
    public async void RunAction(EngineCommand command)
    {
        try{
            if(bridge is null||busy||!ReturnToGame())return;
            var state=bridge.ReadState();if(!state.Ready||!state.Fresh||!state.SceneReady||state.UiFlags!=0)return;
            if(command==EngineCommand.Torch){Preferences.AutoTorch=false;SavePreferences();}
            supply.ManualAction(Environment.TickCount64);await SendAsync(command,0,true);
        }catch(Exception e){if(!closing)ShowError(e.Message);}
    }
    private async Task<bool> SendAsync(EngineCommand command,double arg,bool notify)
    {
        if(bridge is null||busy||closing)return false;busy=true;
        try{
            var state=await bridge.SendAsync(command,arg);
            if(state.Error!=0)throw new InvalidOperationException(state.Error switch{
                3=>"操作已过期，请重新触发。",4=>"操作参数无效。",5=>"请先进入可操作的地图。",6=>"当前头盔没有可开合面甲。",7=>"当前状态或原版装备限制不允许操作。",8=>"请先切回游戏。",9=>"地图或窗口已改变，本次操作已取消。",20=>"随身物品中没有可用饮水。",21=>"随身物品中没有可用火把。",22=>"游戏未确认物品变化；不会自动重复使用。",_=>"此功能当前不可用。"});
            if(notify)UserPreferences.Log($"action {command} confirmed");
            return true;
        }catch(Exception e){if(!closing)ShowError(e.Message);return false;}
        finally{busy=false;}
    }
    public void ToggleFold(){folded=!folded;if(folded)HideHud();}
    public void SavePreferences(){try{Preferences.Save();}catch(Exception e){ShowError("设置保存失败："+e.Message);}}
    public void ChangeBackupFolder(string folder)
    {
        if(Saves.Busy)throw new InvalidOperationException("请等待存档操作完成。");
        var selected=new SaveManagerService(backupRoot:folder);
        // Validate separation before writing even a small writeability probe.
        _=new SaveEngine(selected.SaveRoot,selected.BackupRoot);
        Directory.CreateDirectory(selected.BackupRoot);
        string probe=Path.Combine(selected.BackupRoot,".write-check-"+Guid.NewGuid().ToString("N"));
        using(var file=new FileStream(probe,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,4096,FileOptions.DeleteOnClose)){file.WriteByte(0);file.Flush(true);}
        string? previous=Preferences.BackupFolder;Preferences.BackupFolder=selected.BackupRoot;
        try{Preferences.Save();}catch{Preferences.BackupFolder=previous;throw;}
        Saves=selected;SaveStatus="备份目录已切换；原目录中的历史备份仍保留在原处。";
    }
    public void OpenSettings()
    {
        if(closing)return;
        if(settings is null){settings=new SettingsWindow(this){Owner=hud,Topmost=true};settings.Closed+=(_,_)=>settings=null;settings.Show();}else settings.Activate();
    }
    public void ResetLayout(){Preferences.HasHudPlacement=false;SavePreferences();}
    public void OpenSaves()
    {
        if(closing)return;
        if(savesWindow is null){savesWindow=new SaveWindow(this){Owner=hud,Topmost=true};savesWindow.Closed+=(_,_)=>savesWindow=null;savesWindow.Show();}else savesWindow.Activate();
    }
    public async void Backup(bool latest)=>await SaveAsync(latest?SaveOperation.Latest:SaveOperation.Backup);
    public async Task SaveAsync(SaveOperation operation,string? archive=null)
    {
        if(Saves.Busy||ExitRequested||closing||Updater.Installing)return;
        var progress=new Progress<string>(message=>{SaveStatus=message;savesWindow?.Refresh(false);});
        SaveStatus=operation==SaveOperation.Restore?"正在准备还原…":"正在准备备份…";savesWindow?.Refresh();
        try{
            var result=await Saves.RunAsync(operation,archive,progress);
            SaveStatus=operation==SaveOperation.Restore?$"文件还原完成 · {result.Files} 个文件校验通过；游戏读档尚需确认":$"备份成功 · {DateTime.Now:HH:mm:ss} · {result.Files} 个文件";
            UserPreferences.Log($"save {operation} verified files={result.Files} archive={result.Archive}");
        }catch(Exception e){SaveStatus="存档操作失败："+e.Message;ShowError(SaveStatus);}
        finally{savesWindow?.Refresh();if(ExitRequested)Close();}
    }
    public void RequestExit()
    {
        if(closing)return;
        ExitRequested=true;
        if(Saves.Busy){SetStatus("存档操作完成后自动退出助手…");return;}
        Close();
    }
    public void ShowError(string error){SetStatus(error);UserPreferences.Log("error "+error);tray.BalloonTipTitle="晶石助手";tray.BalloonTipText=error;tray.ShowBalloonTip(3500);}
    private async void OnClosing(object? sender,CancelEventArgs e)
    {
        if(closed)return;e.Cancel=true;if(closing)return;if(Saves.Busy){RequestExit();return;}closing=true;poll.Stop();RegisterKeys(false);HideHud();
        Updater.Dispose();intent.Stop();
        try{if(bridge is not null)await bridge.SendAsync(EngineCommand.Reset);}catch(Exception ex){UserPreferences.Log("exit-reset "+ex.Message);}
        finally{bridge?.Dispose();Native.UnregisterHotKey(hwnd,20);Native.UnregisterHotKey(hwnd,21);tray.Dispose();settings?.Close();savesWindow?.Close();hud.Close();closed=true;_=Dispatcher.BeginInvoke(Close);}
    }
}
