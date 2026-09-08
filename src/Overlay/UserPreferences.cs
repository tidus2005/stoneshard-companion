using System.IO;
using System.Text.Json;

namespace StoneshardCompanion;

public sealed class UserPreferences
{
    public bool AutoCenter { get; set; }
    public bool RememberSpeed { get; set; }
    public int LastSpeed { get; set; } = 1;
    public double Scale { get; set; } = 1;
    public double BottomOffset { get; set; }
    public bool ShowLabels { get; set; }
    public bool AutoDrink {get;set;}
    public bool AutoTorch {get;set;}
    public bool AutoWalkKeys {get;set;}
    public double DrinkThreshold {get;set;}=25;
    public bool HasHudPlacement {get;set;}
    public double HudX {get;set;}=16;
    public double HudY {get;set;}=16;
    public double HudWidth {get;set;}=HudGeometry.DefaultWidth;
    public double HudHeight {get;set;}=HudGeometry.DefaultHeight;
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StoneshardCompanion");
    private static string FileName => Path.Combine(Folder,"preferences.json");
    public static UserPreferences Load()
    {
        try {
            var p=JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(FileName))??new();
            p.LastSpeed=Math.Clamp(p.LastSpeed,1,4);p.Scale=Math.Clamp(p.Scale,.75,1.35);p.BottomOffset=Math.Clamp(p.BottomOffset,-60,180);
            p.DrinkThreshold=double.IsFinite(p.DrinkThreshold)?Math.Clamp(p.DrinkThreshold,10,80):25;
            if(!double.IsFinite(p.HudX)||!double.IsFinite(p.HudY)||!double.IsFinite(p.HudWidth)||!double.IsFinite(p.HudHeight))p.HasHudPlacement=false;
            return p;
        } catch(IOException){return new();} catch(JsonException){return new();}
    }
    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FileName+".tmp",JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(FileName+".tmp",FileName,true);
    }
    public static void Log(string text)
    {
        try {
            Directory.CreateDirectory(Folder);var path=Path.Combine(Folder,"session.log");
            if(File.Exists(path)&&new FileInfo(path).Length>2_000_000)File.Move(path,path+".previous",true);
            File.AppendAllText(path,$"{DateTimeOffset.Now:o} {text}{Environment.NewLine}");
        }catch(IOException){}
    }
}
