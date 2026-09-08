using System.ComponentModel;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace StoneshardCompanion;

public enum EngineCommand : uint { Refresh=1,Speed=2,Center=3,Player=4,Visor=5,Reset=6,Diagnostic=7,AutoCenter=8,Suspend=9,Inspect=10,Drink=11,Torch=12,Highlight=13,Walk=14,HighlightSet=15,WalkKeysSet=16 }
public sealed record EngineState(int Error,uint Capabilities,double BaseSpeed,double TargetSpeed,double Multiplier,double CameraX,double CameraY,double CameraWidth,double CameraHeight,double MapWidth,double MapHeight,double PlayerX,double PlayerY,int CameraMode,int VisorState,ulong Samples,string Detail,string Diagnostic,bool AutoCenter,
    bool SceneReady,ulong SceneGeneration,ulong WindowGeneration,double PreferredMultiplier,uint SuspendReasons,uint UiFlags,double Hunger,double Thirst,double Pain,double Intoxication,double GuiWidth,double GuiHeight,uint VitalValid,int HighlightState,int TorchState,int WaterUses,long UpdatedAt,bool Ready)
{
    public bool Fresh => UpdatedAt>0 && Environment.TickCount64-UpdatedAt is >=0 and <2500;
    public uint SupplyFlags {get;init;}
    public int TorchCount {get;init;}
    public double TorchDuration {get;init;}
    public bool HighlightApplied {get;init;}
    public uint WalkState {get;init;}
    public int WalkDirection {get;init;}
    public uint WalkPhase {get;init;}
    public bool WalkKeysEnabled {get;init;}
    public bool LabelHookReady {get;init;}
    public double WalkX {get;init;}
    public double WalkY {get;init;}
    public ulong LabelDrawCalls {get;init;}
    public ulong LabelDrawOverrides {get;init;}
    public bool SpeedUiAllowed => (UiFlags & (2u|4u|16u|32u)) == 0;
    public static string WalkDestinationName(int direction) => direction switch {
        1=>"上边缘",2=>"下边缘",3=>"左边缘",4=>"右边缘",5=>"地图中心",
        6=>"左上角",7=>"右上角",8=>"左下角",9=>"右下角",
        11=>"人物正上方边缘",12=>"人物正下方边缘",13=>"人物正左方边缘",14=>"人物正右方边缘",_=>"目标位置"
    };
    public string WalkStatus => WalkState switch {
        1=>WalkPhase==1?"正在跨入相邻地图 · 点击九宫格停步":$"正在前往{WalkDestinationName(WalkDirection)} · 点击九宫格停步",
        2=>$"已到达{WalkDestinationName(WalkDirection)}",3=>"行走已停止 · 手动接管",4=>"行走已停止 · 敌人或受伤",5=>"行走已停止 · 原版面板打开",
        6=>"地图变化或加载 · 本次行走结束",7=>"寻路中断、目标或出口不可达",8=>"行走已停止 · 游戏在后台",9=>"行走已停止 · 长时间无进展",_=>"九宫格选择地图目标 · 方向键模式沿人物行列行走"
    };
}

public sealed class EngineBridge : IDisposable
{
    private readonly MemoryMappedFile mapping;
    private readonly MemoryMappedViewAccessor view;
    private readonly SemaphoreSlim commands=new(1,1);
    private readonly Timer heartbeat;
    private readonly FileStream lease;
    private int sequence;
    private bool disposed;
    public GameSession Session {get;}
    private EngineBridge(GameSession session,MemoryMappedFile map,FileStream ownership)
    {
        Session=session;mapping=map;lease=ownership;view=map.CreateViewAccessor(0,4096);
        if(view.ReadUInt32(0)!=Native.Magic || view.ReadUInt32(4)!=7 || view.ReadInt32(8)!=session.Pid || view.ReadInt64(16)!=session.Started){view.Dispose();throw new InvalidDataException("游戏连接校验失败。");}
        sequence=view.ReadInt32(32);
        Heartbeat();heartbeat=new Timer(_=>{try{Heartbeat();}catch(ObjectDisposedException){}},null,200,200);
    }
    private void Heartbeat(){lock(view){if(!disposed)view.Write(24,Environment.TickCount64);}}
    public static async Task<EngineBridge> ConnectAsync(GameSession session,string library,CancellationToken token=default)
    {
        string lockDir=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StoneshardCompanion","Locks");Directory.CreateDirectory(lockDir);
        FileStream ownership;
        try{ownership=new FileStream(System.IO.Path.Combine(lockDir,session.Key+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException){throw new IOException("另一个控制器正在连接此游戏，请先关闭它。");}
        try{
        await session.ValidateAsync(token);
        string name=$"Local\\StoneshardCompanion.v7.{session.Pid}";
        foreach(int version in new[]{1,2,3,4,5,6})try{using var old=MemoryMappedFile.OpenExisting($"Local\\StoneshardCompanion.v{version}.{session.Pid}",MemoryMappedFileRights.Read);throw new NotSupportedException("游戏仍加载旧版助手组件，请在方便时正常退出游戏并重新启动一次。");}catch(FileNotFoundException){}
        MemoryMappedFile? map=null;
        try{map=MemoryMappedFile.OpenExisting(name,MemoryMappedFileRights.ReadWrite);}catch(FileNotFoundException){}
        if(map is null)
        {
            await Task.Run(()=>LoadBridge(session,System.IO.Path.GetFullPath(library)),token);
            for(int n=0;n<40 && map is null;n++){
                token.ThrowIfCancellationRequested();
                try{map=MemoryMappedFile.OpenExisting(name,MemoryMappedFileRights.ReadWrite);}catch(FileNotFoundException){await Task.Delay(50,token);}
            }
        }
        if(map is null)throw new IOException("游戏连接未建立。");
        try{return new EngineBridge(session,map,ownership);}catch{map.Dispose();throw;}
        }catch{ownership.Dispose();throw;}
    }
    public EngineState ReadState()
    {
        lock(view)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            for(int attempt=0;attempt<20;attempt++)
            {
                int first=view.ReadInt32(56);if((first&1)!=0){Thread.Sleep(1);continue;}
                byte[] bytes=new byte[SnapshotSize];view.ReadArray(0,bytes,0,bytes.Length);Thread.MemoryBarrier();
                if(first!=view.ReadInt32(56)){Thread.Sleep(1);continue;}
                return DecodeSnapshot(bytes);
            }
            throw new IOException("游戏状态正在更新，请稍后重试。");
        }
    }
    public const int SnapshotSize=3968;
    public static EngineState DecodeSnapshot(byte[] bytes)
    {
        if(bytes.Length<SnapshotSize)throw new InvalidDataException("游戏状态数据不完整。");
        double D(int at)=>BitConverter.ToDouble(bytes,at);
        int I(int at)=>BitConverter.ToInt32(bytes,at);
        string S(int at,int max){int end=Array.IndexOf(bytes,(byte)0,at,max);return Encoding.UTF8.GetString(bytes,at,(end<0?at+max:end)-at);}
        return new(I(64),BitConverter.ToUInt32(bytes,68),D(72),D(80),D(88),D(96),D(104),D(112),D(120),D(128),D(136),D(144),D(152),I(160),I(164),BitConverter.ToUInt64(bytes,176),S(184,512),S(696,3072),I(3768)==1,
            I(3772)==1,BitConverter.ToUInt64(bytes,3776),BitConverter.ToUInt64(bytes,3784),D(3792),(uint)I(3800),(uint)I(3804),D(3808),D(3816),D(3824),D(3832),D(3840),D(3848),(uint)I(3856),I(3860),I(3864),I(3868),BitConverter.ToInt64(bytes,3872),I(12)==1)
            {SupplyFlags=(uint)I(3896),TorchCount=I(3900),TorchDuration=D(3904),HighlightApplied=I(3912)==1,WalkState=(uint)I(3916),WalkDirection=I(3920),WalkPhase=(uint)I(3960),WalkKeysEnabled=I(3964)==1,LabelHookReady=I(3924)==1,WalkX=D(3928),WalkY=D(3936),LabelDrawCalls=BitConverter.ToUInt64(bytes,3944),LabelDrawOverrides=BitConverter.ToUInt64(bytes,3952)};
    }
    public async Task<EngineState> SendAsync(EngineCommand command,double argument=0,CancellationToken token=default)
    {
        await commands.WaitAsync(token);
        try
        {
            int seq;nint window;
            lock(view){
                ObjectDisposedException.ThrowIf(disposed,this);
                if(view.ReadUInt32(12)!=1)throw new IOException("游戏已断开。");
                window=(nint)view.ReadUInt64(168);Native.GetWindowThreadProcessId(window,out var pid);
                if(!Native.IsWindow(window)||pid!=Session.Pid)throw new IOException("游戏窗口正在恢复。");
                view.Write(36,(uint)command);view.Write(40,argument);view.Write(48,Environment.TickCount64+1500);
                view.Write(3880,view.ReadUInt64(3784));view.Write(3888,view.ReadUInt64(3776));
                seq=++sequence;Thread.MemoryBarrier();view.Write(32,seq);
            }
            if(!Native.PostMessage(window,Native.Message,Native.Magic,0))throw new Win32Exception(Marshal.GetLastWin32Error());
            var sw=Stopwatch.StartNew();
            while(sw.ElapsedMilliseconds<1800){
                token.ThrowIfCancellationRequested();
                lock(view){if(!disposed && view.ReadInt32(60)==seq)return ReadState();}
                await Task.Delay(15,token);
            }
            throw new TimeoutException("游戏未确认操作；不会自动重复执行。");
        }
        finally{commands.Release();}
    }
    private static void LoadBridge(GameSession session,string library)
    {
        if(!File.Exists(library))throw new FileNotFoundException("缺少游戏连接组件。",library);
        using var process=Process.GetProcessById(session.Pid);
        var handle=Native.OpenProcess(0x043A,false,session.Pid);
        if(handle==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"无法访问游戏进程，请确认工具与游戏的权限一致。");
        nint path=0;bool pathSafeToFree=true;
        try
        {
            var module=process.Modules.Cast<ProcessModule>().FirstOrDefault(m=>string.Equals(m.FileName,library,StringComparison.OrdinalIgnoreCase));
            if(module is null)
            {
                var bytes=Encoding.Unicode.GetBytes(library+"\0");
                path=Native.VirtualAllocEx(handle,0,(nuint)bytes.Length,0x3000,4);
                if(path==0 || !Native.WriteProcessMemory(handle,path,bytes,(nuint)bytes.Length,out var written) || written!=(nuint)bytes.Length)throw new Win32Exception(Marshal.GetLastWin32Error());
                var localLoad=Native.GetProcAddress(Native.GetModuleHandle("kernel32.dll"),"LoadLibraryW");
                using var own=Process.GetCurrentProcess();
                var owner=own.Modules.Cast<ProcessModule>().Single(m=>localLoad>=m.BaseAddress && localLoad<m.BaseAddress+m.ModuleMemorySize);
                var remoteOwner=process.Modules.Cast<ProcessModule>().Single(m=>string.Equals(m.ModuleName,owner.ModuleName,StringComparison.OrdinalIgnoreCase));
                try{RunThread(handle,remoteOwner.BaseAddress+(localLoad-owner.BaseAddress),path);}catch(TimeoutException){pathSafeToFree=false;throw;}
                process.Refresh();
                module=process.Modules.Cast<ProcessModule>().FirstOrDefault(m=>string.Equals(m.FileName,library,StringComparison.OrdinalIgnoreCase));
                if(module is null)throw new IOException("游戏未加载连接组件。");
            }
            var local=Native.LoadLibraryEx(library,0,1); // DONT_RESOLVE_DLL_REFERENCES: inspect export only.
            if(local==0)throw new Win32Exception(Marshal.GetLastWin32Error());
            try{
                var export=Native.GetProcAddress(local,"BridgeStart");if(export==0)throw new IOException("游戏组件版本不正确。");
                uint result=RunThread(handle,module.BaseAddress+(export-local),0);
                if(result==15)throw new IOException("游戏引擎正在初始化，稍后自动连接。");
                if(result!=0)throw new IOException($"游戏连接检查失败（{result}），未启用功能。");
            }finally{Native.FreeLibrary(local);}
        }
        finally{if(path!=0&&pathSafeToFree)Native.VirtualFreeEx(handle,path,0,0x8000);Native.CloseHandle(handle);}
    }
    private static uint RunThread(nint process,nint start,nint arg)
    {
        var thread=Native.CreateRemoteThread(process,0,0,start,arg,0,0);
        if(thread==0)throw new Win32Exception(Marshal.GetLastWin32Error());
        try{
            if(Native.WaitForSingleObject(thread,10000)!=0)throw new TimeoutException("游戏组件加载未完成，请勿重复连接。");
            if(!Native.GetExitCodeThread(thread,out var code))throw new Win32Exception(Marshal.GetLastWin32Error());return code;
        }finally{Native.CloseHandle(thread);}
    }
    public void Dispose(){lock(view){if(disposed)return;disposed=true;heartbeat.Dispose();try{view.Write(24,0L);}finally{view.Dispose();mapping.Dispose();lease.Dispose();}}}
}
