using System.Globalization;
using System.Text.RegularExpressions;
namespace StoneshardCompanion;

// Coefficients verified against the in-game 0.9.4.25 primary-attribute tooltips.
// These are contributions, not a claim that every final stat is a simple sum.
public static class StatMechanics
{
    private sealed record Rule(string Attribute,string Name,string Target,double PerPoint,bool Milestone=false);
    private static readonly Rule[] Rules=[
        new("STR","力量","Weapon_Damage",1.5),new("STR","力量","PRR",1.5),new("STR","力量","Block_Power",1),
        new("STR","力量","Bodypart_Damage",7.5,true),new("STR","力量","CRTD",10,true),new("STR","力量","Armor_Damage",15,true),
        new("AGL","敏捷","CTA",1.5),new("AGL","敏捷","FMB",-1.5),new("AGL","敏捷","Miscast_Chance",-1.5),
        new("AGL","敏捷","EVS",5,true),new("AGL","敏捷","Mainhand_Efficiency",2.5,true),new("AGL","敏捷","Offhand_Efficiency",2.5,true),
        new("AGL","敏捷","Knockback_Resistance",7.5,true),
        new("PRC","感知","Hit_Chance",1.5),new("PRC","感知","Armor_Piercing",1.5),new("PRC","感知","Spell_Armor_Piercing",1.5),
        new("PRC","感知","CRT",5,true),new("PRC","感知","Miracle_Chance",5,true),
        new("Vitality","活力","max_mp",4),new("Vitality","活力","MP_Restoration",2),
        new("Vitality","活力","max_hp",15,true),new("Vitality","活力","Block_Recovery",5,true),
        new("WIL","意志","Cooldown_Reduction",-1.5),new("WIL","意志","Abilities_Energy_Cost",-1.5),
        new("WIL","意志","Magic_Power",7.5,true),new("WIL","意志","Pain_Resistance",7.5,true),new("WIL","意志","Fortitude",7.5,true)
    ];
    public static string CleanLabel(string text)=>Regex.Replace(text,@"~/?[A-Za-z]*~","");
    public static IEnumerable<(string Label,double Delta)> Contributions(CharacterTelemetry data,string key){
        foreach(var rule in Rules.Where(r=>r.Target==key)){
            double? value=data.Stats.FirstOrDefault(s=>s.Key==rule.Attribute)?.Value;
            if(value is null||value<10||value>30)continue;
            double steps=rule.Milestone?Math.Floor((value.Value-10)/5):value.Value-10;
            string detail=rule.Milestone?$"已达到 {steps:0} 个 15／20／25／30 阈值":$"({value:0.##} − 10) × {rule.PerPoint:0.##}";
            yield return ($"{rule.Name} {value:0.##}：{detail}",steps*rule.PerPoint);
        }
    }
    public static string WeaponEquation(CharacterTelemetry data){
        double? value=data.Stats.FirstOrDefault(s=>s.Key=="Weapon_Damage")?.Value;
        double? strength=data.Stats.FirstOrDefault(s=>s.Key=="STR")?.Value;
        if(value is null||strength is null||strength<10||strength>30)return "";
        double primary=(strength.Value-10)*1.5;
        // Do not attribute the unexplained residual to an invented injury/skill.
        double remaining=value.Value-100-primary;
        string n(double x)=>x.ToString("0.##",CultureInfo.InvariantCulture);
        return $"兵器伤害：100% 基准 + {n(primary)} 个百分点（力量） + {n(remaining)} 个百分点（其余修正净额） = {n(value.Value)}%。\n其余修正净额仅是差值，具体技能、状态与装备以游戏来源记录为准；存在乘算时不能逐项直接相加。";
    }
}
