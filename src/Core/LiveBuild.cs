using System.Text.Json;
using System.Text.RegularExpressions;

namespace StoneshardCompanion;

public sealed record LiveBuildSkill(int Id,string Key,int Category,bool Learned,string Reason,string? Name=null);
public sealed record LiveBuildSnapshot(string Character,string Token,int Crowns,int[] Attributes,int[] Baseline,LiveBuildSkill[] Skills,
    int Credits,int Contracts,int Ledger,int[] Gems,bool SafePlace,string Place,string[] AttributeReasons);
public sealed record RefundRecipe(int Id,string Name,bool? Attribute,int[] Materials);
public sealed record PointRefund(bool Attribute,int Id,int Recipe)
{
    public string Payload(LiveBuildSnapshot s)=>$"{s.Token}|{(Attribute?'A':'S')}|{Id}|{Recipe}";
}
public static class LiveBuild
{
    public const string LedgerKey="companionGemRefundV1";
    public static readonly string[] GemKeys=["o_inv_jade","o_inv_topaz","o_inv_amethyst","o_inv_aquamarine","o_inv_ruby","o_inv_emerald","o_inv_sapphire","o_inv_diamond"];
    public static readonly string[] GemNames=["翡翠","黄玉","紫水晶","海蓝宝石","红宝石","祖母绿","蓝宝石","钻石"];
    public static readonly RefundRecipe[] Recipes=[new(0,"积攒 · 属性",true,[2,1,0,0,0,0,0,0]),new(1,"积攒 · 技能",false,[0,0,2,2,0,0,0,0]),new(2,"红宝石 · 属性",true,[0,0,0,0,1,0,0,0]),new(3,"祖母绿 · 属性",true,[0,0,0,0,0,1,0,0]),new(4,"蓝宝石 · 技能",false,[0,0,1,0,0,0,1,0]),new(5,"钻石 · 通用",null,[0,0,0,0,0,0,0,1])];
    public static string Cost(RefundRecipe r)=>string.Join(" ＋ ",r.Materials.Select((n,i)=>(n,i)).Where(x=>x.n>0).Select(x=>$"{GemNames[x.i]} ×{x.n}"));
    public static string Missing(LiveBuildSnapshot s,RefundRecipe r)=>string.Join("、",r.Materials.Select((n,i)=>(n:Math.Max(0,n-s.Gems[i]),i)).Where(x=>x.n>0).Select(x=>$"{GemNames[x.i]} ×{x.n}"));
    public static IEnumerable<RefundRecipe> Options(bool attribute)=>Recipes.Where(r=>r.Attribute is null||r.Attribute==attribute);
    public static LiveBuildSnapshot Parse(string json){
        using var doc=JsonDocument.Parse(json);
        if(doc.RootElement.TryGetProperty("error",out var error))throw new IOException(error.GetString());
        var s=JsonSerializer.Deserialize<LiveBuildSnapshot>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new IOException("未读到角色。");
        if(s.Attributes is not {Length:8}||s.Baseline is not {Length:5}||s.Skills is not {Length:>0}||!Regex.IsMatch(s.Token??"","^[0-9a-f]{16}$")||s.Crowns<0||s.Attributes.Any(x=>x<0)||s.Skills.Select(x=>x.Id).Distinct().Count()!=s.Skills.Length||s.Gems is not {Length:8}||s.Gems.Any(x=>x<0||x>128)||s.AttributeReasons is not {Length:5}||s.Credits<0||s.Credits>6||s.Contracts<0||s.Contracts>1000000||s.Ledger!=1000000000+s.Contracts*10+s.Credits)throw new IOException("角色或历练数据不完整，请使用新版助手组件。");
        return s;
    }
    public static void Validate(LiveBuildSnapshot s,PointRefund r){
        if(r.Attribute){if(r.Id<0||r.Id>=5||s.Attributes[r.Id]<=s.Baseline[r.Id])throw new IOException("不能退回角色初始属性。");if(s.AttributeReasons[r.Id].Length>0)throw new IOException(s.AttributeReasons[r.Id]);}
        else{var skill=s.Skills.SingleOrDefault(x=>x.Id==r.Id);if(skill is null||!skill.Learned||skill.Reason.Length>0)throw new IOException("请先退回后续技能；此技能当前不可退回。");}
        if(!s.SafePlace)throw new IOException("请回到城镇或马车营地再退点。");
        if(s.Credits<1)throw new IOException("历练机会不足：完成并交付一个契约可获得 2 次机会。");
        var recipe=Recipes.SingleOrDefault(x=>x.Id==r.Recipe)??throw new IOException("未知材料配方。");
        if(recipe.Attribute is not null&&recipe.Attribute!=r.Attribute)throw new IOException("此配方不适用于这类点数。");
        string missing=Missing(s,recipe);if(missing.Length>0)throw new IOException("主背包缺少："+missing);
    }
    public static void Verify(LiveBuildSnapshot before,LiveBuildSnapshot after,PointRefund r){
        var expected=(int[])before.Attributes.Clone();expected[r.Attribute?r.Id:6]+=r.Attribute?-1:1;if(r.Attribute)expected[5]++;
        var recipe=Recipes.Single(x=>x.Id==r.Recipe);
        if(before.Character!=after.Character||after.Crowns!=before.Crowns||after.Credits!=before.Credits-1||after.Contracts!=before.Contracts||after.Ledger!=before.Ledger-1||!after.Gems.SequenceEqual(before.Gems.Select((n,i)=>n-recipe.Materials[i]))||!expected.SequenceEqual(after.Attributes)||before.Skills.Length!=after.Skills.Length||before.Skills.Any(x=>after.Skills.SingleOrDefault(y=>y.Id==x.Id)?.Learned!=(x.Learned&&(r.Attribute||x.Id!=r.Id))))throw new IOException("退点后的宝石、历练或点数不符合预期。");
    }
    public static void VerifySavedMaterials(string path,LiveBuildSnapshot after){
        // Decode the same native slot already verified by BuildEditor.Read.
        string slot=Path.GetFileName(Path.GetDirectoryName(path)!),character=Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)!)!);
        var data=PaidRespec.Decode(File.ReadAllBytes(path),$"stOne!characters_v1!{character}!{slot}!shArd");
        int ledger=checked((int)(data["characterDataMap"]?[LedgerKey]?.GetValue<double>()??-1));
        var counts=new int[8];foreach(var node in data["inventoryDataList"]!.AsArray()){string key=node![0]!.GetValue<string>();int i=Array.IndexOf(GemKeys,key);if(i>=0)counts[i]++;}
        if(ledger!=after.Ledger||!counts.SequenceEqual(after.Gems))throw new IOException("保存后的材料或历练机会未通过核验。");
    }
}
