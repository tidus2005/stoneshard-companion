using System.Globalization;
using System.Text;
using System.Text.Json;
namespace StoneshardCompanion;
public sealed record StatReading(string Key,double? Value,double? Baseline);
public sealed record StatSource(string Key,double? Delta,double? SourceId,string Label);
public sealed record FodderMaterial(string Key,string Name,double Value);
public sealed class CharacterTelemetry
{
    public long At {get;set;}
    public double? Player {get;set;}
    public StatReading[] Stats {get;set;}=[];
    public StatSource[] Sources {get;set;}=[];
    public FodderMaterial[] Foods {get;set;}=[];
    public bool FoodsComplete {get;set;}
    public bool SourcesAvailable {get;set;}
    public bool SourcesTruncated {get;set;}
    public JourneyTelemetry Journey {get;set;}=new();
    public bool Fresh(long now)=>At>0&&now>=At&&now-At<3000;
    public static CharacterTelemetry Parse(string json){
        if(string.IsNullOrWhiteSpace(json)||json.Length>57344)return new();
        try{
            var data=JsonSerializer.Deserialize<CharacterTelemetry>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true,MaxDepth=8})??new();
            if(data.Stats is null||data.Sources is null||data.Foods is null||data.Stats.Length>256||data.Sources.Length>256||data.Foods.Length>256)return new();
            if(data.Journey is null||data.Journey.Provisions is null||data.Journey.Equipment is null||data.Journey.Provisions.Length>256||data.Journey.Equipment.Length>64||!Finite(data.Journey.Minutes))return new();
            if(data.Journey.Provisions.Any(p=>p is null||p.Name is null||!double.IsFinite(p.Uses)||p.Uses<0||p.Uses>10000||!Finite(p.Hunger)||!Finite(p.Thirst)||!Finite(p.FreshHours))||data.Journey.Equipment.Any(e=>e is null||e.Name is null||!double.IsFinite(e.Current)||!double.IsFinite(e.Maximum)||e.Maximum<=0))return new();
            if(data.Stats.Any(s=>s is null||s.Key is null||s.Key.Length>120||!Finite(s.Value)||!Finite(s.Baseline))||data.Sources.Any(s=>s is null||s.Key is null||s.Label is null||!Finite(s.Delta)||!Finite(s.SourceId))||data.Foods.Any(f=>f is null||!FodderPolicy.ValidKey(f.Key)||!double.IsFinite(f.Value)||f.Value<=0))return new();
            return data;
        }catch(JsonException){return new();}
    }
    private static bool Finite(double? v)=>v is null||double.IsFinite(v.Value);
    public static string Format(double? v,string unit)=>v is null?"—":v.Value.ToString("0.##",CultureInfo.InvariantCulture)+unit;
    public string Explain(StatDefinition definition){
        var stat=Stats.FirstOrDefault(s=>s.Key==definition.Key);var b=new StringBuilder();
        b.AppendLine(definition.Name+" · 游戏实时值");b.AppendLine("当前："+Format(stat?.Value,definition.Unit));
        b.AppendLine("原生基础值："+Format(stat?.Baseline,definition.Unit));
        if(stat?.Baseline is not null&&stat.Value is not null)b.AppendLine("相对基础值净变化："+(stat.Value-stat.Baseline>=0?"+":"")+Format(stat.Value-stat.Baseline,definition.Unit=="%"?" 个百分点":definition.Unit));
        var primary=StatMechanics.Contributions(this,definition.Key).ToArray();
        if(primary.Length>0){b.AppendLine();b.AppendLine("基础属性影响（游戏内规则）：");foreach(var p in primary)b.AppendLine($"• {p.Label} = {(p.Delta>=0?"+":"")}{Format(p.Delta,definition.Unit=="%"?" 个百分点":definition.Unit)}");}
        if(definition.Key=="Weapon_Damage"){b.AppendLine();b.AppendLine(StatMechanics.WeaponEquation(this));}
        b.AppendLine();b.AppendLine("游戏记录的来源（原始修正值）：");
        var sources=Sources.Where(s=>s.Key==definition.Key).ToArray();
        foreach(var source in sources)b.AppendLine($"• {(string.IsNullOrWhiteSpace(source.Label)?$"效果来源 #{source.SourceId:0}":StatMechanics.CleanLabel(source.Label))}：{Format(source.Delta,"")}");
        if(sources.Length==0)b.AppendLine(SourcesAvailable?"暂无可对应到此属性的来源明细。":"当前无法读取原生来源明细。");
        if(SourcesTruncated)b.AppendLine("来源列表已截断，未显示的条目不能视为零加成。");
        b.AppendLine();b.AppendLine("来源值的正负、比例与最终增减由各属性公式决定；例如疲劳增长减免的正值表示减免。来源列表可能包含装备、技能与状态，不能与基础属性贡献重复相加。乘算、上限、取整及未记录来源不作猜测。");
        if(stat?.Baseline is null)b.AppendLine("此项基线尚不可用，未用第一次观测值代替。");
        return b.ToString().TrimEnd();
    }
}
public static class FodderPolicy
{
    public static bool ValidKey(string? s)=>!string.IsNullOrEmpty(s)&&s.Length<=120&&s.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_');
    public static string Selection(IEnumerable<string> keys){var selected=keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();if(selected.Any(k=>!ValidKey(k)))throw new ArgumentException("无效材料标识");string result="|"+string.Join("||",selected)+"|";if(Encoding.UTF8.GetByteCount(result)>=2048)throw new ArgumentException("材料选择过多");return result;}
}
