using System.Text.Json;
namespace StoneshardCompanion;

public sealed record CombatBuff(string Name);
public sealed class CombatTelemetry
{
    public long At {get;set;}
    public ulong Scene {get;set;}
    public double TargetId {get;set;}=-1;
    public string Name {get;set;}="";
    public int CursorX {get;set;}
    public int CursorY {get;set;}
    public double? Distance {get;set;}
    public bool SkillSelected {get;set;}
    public string SkillName {get;set;}="";
    public Dictionary<string,double> Player {get;set;}=[];
    public Dictionary<string,double> Target {get;set;}=[];
    public CombatBuff[] Buffs {get;set;}=[];
    public bool BuffsComplete {get;set;}
    public bool Fresh(long now,ulong scene)=>At>0&&now>=At&&now-At<=450&&Scene==scene&&TargetId>=0;
    public static CombatTelemetry Parse(string json){
        if(string.IsNullOrWhiteSpace(json)||json.Length>8192)return new();
        try{
            var s=JsonSerializer.Deserialize<CombatTelemetry>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true,MaxDepth=5});
            if(s is null||s.Name is null||s.SkillName is null||s.Name.Length>256||s.SkillName.Length>256||!double.IsFinite(s.TargetId)||s.TargetId<0||s.Distance is not double distance||!double.IsFinite(distance)||distance<0||distance>10000||s.Player is null||s.Target is null||s.Buffs is null||s.Buffs.Length>24||s.Buffs.Any(b=>b is null||b.Name is null||b.Name.Length>256))return new();
            if(new[]{s.Player,s.Target}.Any(d=>d.Count>128||d.Any(p=>!FodderPolicy.ValidKey(p.Key)||!double.IsFinite(p.Value)||Math.Abs(p.Value)>1e9)))return new();
            return s;
        }catch(JsonException){return new();}
    }
}
public sealed record MeleeForecast(double AccuracyRoll,double DodgeRoll,double Clean,double Graze,double Miss);
public sealed record CombatRow(string Label,string Value,bool Highlight=false);
public sealed record CombatSection(string Title,IReadOnlyList<CombatRow> Rows);
public static class CombatPreview
{
    public static double? Value(IReadOnlyDictionary<string,double> stats,string key)=>stats.TryGetValue(key,out double v)&&double.IsFinite(v)?v:null;
    // Basic melee only. Dodge converts a clean hit into a fumble, and a fumble
    // into a miss. Crits and blocking are subsequent, separate checks.
    public static MeleeForecast? Melee(IReadOnlyDictionary<string,double> attacker,IReadOnlyDictionary<string,double> defender){
        double? accuracy=Value(attacker,"Hit_Chance"),dodge=Value(defender,"EVS"),fumble=Value(attacker,"FMB");
        if(accuracy is null||dodge is null||fumble is null)return null;
        double a=Math.Clamp(accuracy.Value-Math.Min(0,dodge.Value),0,100)/100;
        double d=Math.Clamp(dodge.Value-Math.Max(0,accuracy.Value-100),0,100)/100;
        double f=Math.Clamp(fumble.Value,0,100)/100;
        double clean=a*(1-f)*(1-d),graze=a*(f*(1-d)+(1-f)*d);
        return new(a*100,d*100,clean*100,graze*100,Math.Clamp((1-clean-graze)*100,0,100));
    }
    public static string Format(double? value,string unit="")=>CharacterTelemetry.Format(value,unit);
    private static CombatRow Row(IReadOnlyDictionary<string,double> stats,string key,string? label=null){var d=StatsCatalog.All.FirstOrDefault(s=>s.Key==key);return new(label??(key=="Hit_Chance"?"准确率":key=="Stun_Resistance"?"控制抗性":d?.Name)??key,Format(Value(stats,key),d?.Unit??""));}
    private static void Forecast(List<CombatRow> rows,IReadOnlyDictionary<string,double> attack,IReadOnlyDictionary<string,double> defend,bool unsupported){
        if(unsupported){rows.Add(new("概率预估","此攻击类型尚未适配"));return;}
        var f=Melee(attack,defend);
        if(f is null){rows.Add(new("概率预估","缺少准确 / 闪避 / 失手数据"));return;}
        rows.Add(new("有效准确 / 闪避",$"{f.AccuracyRoll:0.#}% / {f.DodgeRoll:0.#}%"));
        rows.Add(new("正常或暴击命中",$"≈ {f.Clean:0.#}%（格挡前）",true));
        rows.Add(new("擦伤 / 完全落空",$"≈ {f.Graze:0.#}% / {f.Miss:0.#}%"));
    }
    public static CombatSection[] Describe(CombatTelemetry s,CharacterTelemetry character,bool details){
        var attack=new List<CombatRow>();var target=new List<CombatRow>();var defense=new List<CombatRow>();
        attack.Add(new("当前选择",s.SkillSelected?(string.IsNullOrWhiteSpace(s.SkillName)?"技能瞄准":StatMechanics.CleanLabel(s.SkillName)):"普通攻击",true));
        if(s.SkillSelected)attack.Add(new("技能预估","未适配技能特效；以下为角色面板值"));
        foreach(string key in new[]{"Hit_Chance","FMB","CRT","DMG","offDMG","Armor_Piercing","Armor_Damage"})attack.Add(Row(s.Player,key));
        Forecast(attack,s.Player,s.Target,s.SkillSelected||Value(s.Player,"is_range")!=0);
        attack.Add(new("伤害口径","主/副手为面板伤害，未扣护甲与抗性"));
        attack.Add(new("护甲损伤","破坏耐久，与本次穿透分开计算"));
        if(character.Fresh(s.At)&&character.SourcesAvailable){
            var keys=new HashSet<string>{"Hit_Chance","FMB","CRT","DMG","Weapon_Damage","Armor_Piercing","Armor_Damage","EVS","PRR"};
            var sources=character.Sources.Where(x=>keys.Contains(x.Key)).ToArray();int limit=details?12:4;
            foreach(var source in sources.Take(limit)){
                var label=StatsCatalog.All.FirstOrDefault(x=>x.Key==source.Key)?.Name??source.Key;
                attack.Add(new(StatMechanics.CleanLabel(source.Label),$"{label} {Format(source.Delta)}"));
            }
            if(sources.Length>limit||character.SourcesTruncated)attack.Add(new("状态来源","明细较多；完整拆解见左侧属性面板"));
            attack.Add(new("叠加口径","上方当前值已含状态；来源不再重复相加"));
        }else attack.Add(new("状态来源","当前来源明细不可用"));
        target.Add(new("生命",$"{Format(Value(s.Target,"HP"))} / {Format(Value(s.Target,"max_hp"))}",true));
        target.Add(new("能量",$"{Format(Value(s.Target,"MP"))} / {Format(Value(s.Target,"max_mp"))}"));
        target.Add(Row(s.Target,"DEF","护甲防护"));target.Add(Row(s.Target,"ArmorDurability","护甲耐久（原始值）"));
        foreach(string key in new[]{"EVS","PRR","Block_Power","CTA","Physical_Resistance","Magic_Resistance","Nature_Resistance","Stun_Resistance","Bleeding_Resistance"})target.Add(Row(s.Target,key));
        if(details)foreach(var d in StatsCatalog.All.Where(d=>d.Key.EndsWith("_Resistance")&&!target.Any(r=>r.Label==d.Name)))target.Add(Row(s.Target,d.Key));
        foreach(var buff in s.Buffs.Take(details?24:6))target.Add(new("状态 / 伤势",StatMechanics.CleanLabel(buff.Name)));
        if(s.Buffs.Length==0||!s.BuffsComplete||s.Buffs.Length>6&&!details)target.Add(new("状态列表",!s.BuffsComplete?"部分条目未读取；不等于没有负面状态":s.Buffs.Length==0?"未见活动状态":$"共 {s.Buffs.Length} 项，按住 Alt 展开"));
        target.Add(new("部位伤势 / 护甲","细分数据尚未适配，请用原生查看核对"));
        defense.Add(new("情景","假设对方用当前属性普通近战攻击"));
        defense.Add(new("时间口径","当前快照；不推测出招后状态变化"));
        foreach(string key in new[]{"Hit_Chance","FMB","DMG","Armor_Piercing","Armor_Damage"})defense.Add(Row(s.Target,key,"敌方"+(StatsCatalog.All.First(d=>d.Key==key).Name)));
        foreach(string key in new[]{"EVS","PRR","Block_Power","Crit_Avoid","Physical_Resistance"})defense.Add(Row(s.Player,key,"我的"+StatsCatalog.All.First(d=>d.Key==key).Name));
        Forecast(defense,s.Target,s.Player,Value(s.Target,"is_range")!=0||Value(s.Target,"is_mage")!=0);
        defense.Add(new("格挡说明","格挡成功仍可能受伤，取决于剩余格挡能量"));
        defense.Add(new("敌方下回合","技能、走位与是否能行动不作预测"));
        return [new("我 → 目标",attack),new("目标当前状态",target),new("目标 → 我",defense)];
    }
    public static (double X,double Y) Place(double mouseX,double mouseY,double width,double height,double left,double top,double right,double bottom){
        double x=Math.Clamp(mouseX-width/2,left,Math.Max(left,right-width));
        double y=mouseY-height-24;if(y<top)y=mouseY+28;
        return(x,Math.Clamp(y,top,Math.Max(top,bottom-height)));
    }
}
