using System.Diagnostics;
using System.Security.Cryptography;

namespace StoneshardCompanion;

public sealed record GameSession(int Pid, long Started, nint Window, string Path, string Version)
{
    public const string SupportedHash = "D45797505F04B9112C5FBB8E85C0F8CDA2FA60AEE954B4E108BAD8A0DC514AFA";
    public string Key => $"{Pid}-{Started}";
    public bool IsForeground
    {
        get { var h=Native.GetForegroundWindow();Native.GetWindowThreadProcessId(h,out var pid);return h!=0 && pid==Pid && !Native.IsIconic(Window); }
    }
    public static IReadOnlyList<GameSession> Discover()
    {
        var result=new List<GameSession>();
        foreach(var p in Process.GetProcessesByName("StoneShard"))
        using(p)
        {
            try {
                var path=p.MainModule?.FileName;
                if(path is null || p.MainWindowHandle==0)continue;
                result.Add(new(p.Id,p.StartTime.ToFileTimeUtc(),p.MainWindowHandle,path,FileVersionInfo.GetVersionInfo(path).FileVersion??"未知"));
            }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
        }
        return result;
    }
    public async Task ValidateAsync(CancellationToken token=default)
    {
        using var p=Process.GetProcessById(Pid);
        if(p.StartTime.ToFileTimeUtc()!=Started || !string.Equals(p.MainModule?.FileName,Path,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("游戏会话已改变，请重新连接。");
        await using var file=File.OpenRead(Path);
        var hash=Convert.ToHexString(await SHA256.HashDataAsync(file,token));
        if(hash!=SupportedHash)throw new NotSupportedException($"当前游戏 {Version} 尚未适配，已停止连接。");
        await using var data=File.OpenRead(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!,"data.win"));
        var dataHash=Convert.ToHexString(await SHA256.HashDataAsync(data,token));
        if(dataHash!="84034525FCDEF3C6FD9803628B8DE6C43FEA479C930E168A95906DE37A2AE19C" && dataHash!="53604DE8FE39EA37FEE143FBAE4415045F94BFD74B9D40B7B668660F10A53F85")
            throw new NotSupportedException("游戏数据包与已适配版本不同，请使用原版 0.9.4.25。");
    }
}
