using System.Diagnostics;
using System.Security.Cryptography;

namespace StoneshardCompanion;

public sealed record GameSession(int Pid, long Started, nint Window, string Path, string Version)
{
    public const string SupportedHash = "D45797505F04B9112C5FBB8E85C0F8CDA2FA60AEE954B4E108BAD8A0DC514AFA";
    public const string UncappedHash = "2154985DEAB44A615C333173FACC10335D4328E6D1FE90C08AAFD82255BD71EC";
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
        await using var data=File.OpenRead(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!,"data.win"));
        var dataHash=Convert.ToHexString(await SHA256.HashDataAsync(data,token));
        string? rejection=GameCompatibility.Rejection(Version,hash,dataHash,GameCompatibility.InspectRuntime(data));
        if(rejection is not null)throw new NotSupportedException(rejection+GameCompatibility.Fingerprints(hash,dataHash));
    }
}
