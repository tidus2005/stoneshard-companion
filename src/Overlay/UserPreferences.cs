using System.IO;
using System.Text.Json;

namespace StoneshardCompanion;

public sealed class UserPreferences
{
    public double HudOpacity {get;set;}=.75;
    public double StatsOpacity {get;set;}=.55;
    public bool HudBorder {get;set;}
    public bool StatsBorder {get;set;}
    public double StatsWidth {get;set;}=290;
    public double StatsHeight {get;set;}=380;
    public double StatsFontSize {get;set;}=12;
    public double HungerPerHour {get;set;}
    public double ThirstPerHour {get;set;}
    public double DurabilityWarning {get;set;}=25;
    public bool ShowStats {get;set;}=true;
    public bool StatsFavoritesOnly {get;set;}
    public HashSet<string> FavoriteStats {get;set;}=[];
    public HashSet<string> FodderMaterials {get;set;}=[];
    public bool StatsPlaced {get;set;}
    public double StatsX {get;set;}
    public double StatsY {get;set;}
    public bool AutoUpdate {get;set;}=true;
    public bool AutoCenter { get; set; }
    public bool RememberSpeed { get; set; }
    public int LastSpeed { get; set; } = 1;
    public double Scale { get; set; } = 1;
    public double BottomOffset { get; set; }
    public bool ShowLabels { get; set; }
    public bool AutoDrink {get;set;}
    public bool AutoTorch {get;set;}
    public bool AutoWalkKeys {get;set;}
    public string? BackupFolder {get;set;}
    public double DrinkThreshold {get;set;}=25;
    public bool HasHudPlacement {get;set;}
    public double HudX {get;set;}=16;
    public double HudY {get;set;}=16;
    public double HudWidth {get;set;}=HudGeometry.DefaultWidth;
    public double HudHeight {get;set;}=HudGeometry.DefaultHeight;
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),App.UiTestMode?"StoneshardCompanion-UiTest":"StoneshardCompanion");
    private static string FileName => Path.Combine(Folder,"preferences.json");
    public static UserPreferences Load()
    {
        try {
            var p=JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(FileName))??new();
            static double Clamp(double v,double min,double max,double fallback)=>double.IsFinite(v)?Math.Clamp(v,min,max):fallback;
            p.HungerPerHour=Clamp(p.HungerPerHour,0,50,0);p.ThirstPerHour=Clamp(p.ThirstPerHour,0,50,0);
            p.HudOpacity=Clamp(p.HudOpacity,0,1,.75);p.StatsOpacity=Clamp(p.StatsOpacity,0,1,.55);
            p.StatsWidth=Clamp(p.StatsWidth,250,600,290);p.StatsHeight=Clamp(p.StatsHeight,200,900,380);
            p.StatsFontSize=Clamp(p.StatsFontSize,10,20,12);p.DurabilityWarning=Clamp(p.DurabilityWarning,5,75,25);
            p.FavoriteStats=(p.FavoriteStats??[]).Where(k=>StatsCatalog.All.Any(d=>d.Key==k)).ToHashSet(StringComparer.Ordinal);
            p.FodderMaterials=(p.FodderMaterials??[]).Where(FodderPolicy.ValidKey).Take(32).ToHashSet(StringComparer.Ordinal);
            if(!double.IsFinite(p.StatsX)||!double.IsFinite(p.StatsY))p.StatsPlaced=false;
            p.LastSpeed=Math.Clamp(p.LastSpeed,1,4);p.Scale=Math.Clamp(p.Scale,.75,1.35);p.BottomOffset=Math.Clamp(p.BottomOffset,-60,180);
            p.DrinkThreshold=double.IsFinite(p.DrinkThreshold)?Math.Clamp(p.DrinkThreshold,10,80):25;
            if(!double.IsFinite(p.HudX)||!double.IsFinite(p.HudY)||!double.IsFinite(p.HudWidth)||!double.IsFinite(p.HudHeight))p.HasHudPlacement=false;
            if(p.BackupFolder is not null){try{p.BackupFolder=Path.IsPathFullyQualified(p.BackupFolder)?Path.GetFullPath(p.BackupFolder):null;}catch(ArgumentException){p.BackupFolder=null;}}
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
