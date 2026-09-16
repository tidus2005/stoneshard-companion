namespace StoneshardCompanion;

public static class StatPresentation
{
    // Direction follows the stored quantity, not whether its key says Reduction.
    public static int Direction(string key)=>key switch {
        "FMB" or "Damage_Received" or "Hunger" or "Thirsty" or "Pain" or "Intoxication" or "Fatigue" or "Weight" or
        "Skills_Energy_Cost" or "Spells_Energy_Cost" or "Abilities_Energy_Cost" or "Cooldown_Reduction" or
        "Miscast_Chance" or "Backfire_Damage" or "Noise_Produced" => -1,
        "Fatigue_Gain" => 0, // This field's reduction-vs-rate convention is not verified.
        _ => StatsCatalog.All.Any(s=>s.Key==key)?1:0
    };
    public static int Benefit(string key,double? delta)=>delta is double d&&double.IsFinite(d)&&Math.Abs(d)>=.005?Math.Sign(d)*Direction(key):0;
    public static string Delta(double? delta,string unit)=>delta is not double d||!double.IsFinite(d)?"—":
        (Math.Abs(d)<.005?"±0":(d>0?"+":"")+CharacterTelemetry.Format(d,""))+(unit=="%"?" pp":unit);
    public static string Meaning(string key,double? delta)=>delta is null?"基线未知":Benefit(key,delta) switch {
        1=>"✓ 改善",-1=>"! 变差",_=>Math.Abs(delta.Value)<.005?"＝ 无变化":"◇ 仅显示变化，方向待确认"
    };
}
