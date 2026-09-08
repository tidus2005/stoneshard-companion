namespace StoneshardCompanion;

/// <summary>The HUD cycles three speeds; the legacy 4x hotkey remains compatible.</summary>
public static class SpeedControl
{
    public static int Next(int current) => current is 1 or 2 ? current+1 : 1;
    public static string Name(int current) => current switch {1=>"正常",2=>"加速",3=>"急速",_=>"兼容速度"};
    public static string Icon(int current) => current switch {1=>"speed-normal",2=>"speed-fast",_=>"speed-rapid"};
}
