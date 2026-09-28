using System.Text.Json;
using System.Text.RegularExpressions;

namespace StoneshardCompanion;

public sealed record LiveBuildSkill(int Id,string Key,int Category,bool Learned,string Reason,string? Name=null);
public sealed record LiveBuildSnapshot(string Character,string Token,int Crowns,int[] Attributes,int[] Baseline,LiveBuildSkill[] Skills,
    int Credits,int Contracts,int Ledger,int[] Gems,bool SafePlace,string Place,string[] AttributeReasons);
public sealed record PointRefund(bool Attribute,int Id,int[] Materials)
{
    public string Payload(LiveBuildSnapshot s){LiveBuild.Validate(s,this);return $"{s.Token}|{(Attribute?'A':'S')}|{Id}|{string.Join(",",Materials)}";}
}
public static class LiveBuild
{
    public const string LedgerKey="companionGemRefundV1";
    public const int RefundValue=600, GemLimit=127;
    public static readonly string[] GemKeys=["o_inv_jade","o_inv_topaz","o_inv_amethyst","o_inv_aquamarine","o_inv_ruby","o_inv_emerald","o_inv_sapphire","o_inv_diamond","o_inv_amber","o_inv_agate","o_inv_turquoise","o_inv_moonstone","o_inv_morion","o_inv_seapearl"];
    public static readonly string[] GemNames=["翡翠","黄玉","紫水晶","海蓝宝石","红宝石","祖母绿","蓝宝石","钻石","琥珀","玛瑙","绿松石","月光石","烟晶","海水珍珠"];
    // Fixed base prices from the supported game's item table, not merchant quotes.
    public static readonly int[] GemValues=[125,175,150,200,375,450,525,600,40,60,80,100,250,300];
    public static bool RefundUiClear(uint flags)=>(flags&~8u)==0; // A hover tooltip cannot change the allocation.
    public static string PointSummary(LiveBuildSnapshot before,LiveBuildSnapshot after)=>$"属性点 {before.Attributes[5]} → {after.Attributes[5]}；技能点 {before.Attributes[6]} → {after.Attributes[6]}";
    private static void CheckMaterials(int[]? materials){if(materials is null||materials.Length!=GemKeys.Length||materials.Any(n=>n<0||n>GemLimit))throw new IOException("宝石数量无效，请刷新后重新选择。");}
    public static int MaterialValue(int[] materials){CheckMaterials(materials);return materials.Select((n,i)=>n*GemValues[i]).Sum();}
    public static string Cost(int[] materials){CheckMaterials(materials);return string.Join(" ＋ ",materials.Select((n,i)=>(n,i)).Where(x=>x.n>0).Select(x=>$"{GemNames[x.i]} ×{x.n}"));}
    public static string Missing(LiveBuildSnapshot s,int[] materials){CheckMaterials(s.Gems);CheckMaterials(materials);return string.Join("、",materials.Select((n,i)=>(n:Math.Max(0,n-s.Gems[i]),i)).Where(x=>x.n>0).Select(x=>$"{GemNames[x.i]} ×{x.n}"));}
    public static int[]? SuggestMaterials(int[] inventory){
        CheckMaterials(inventory);
        // Bounded knapsack: least total value >=600, then fewest stones.
        // A minimum crossing never exceeds threshold + largest price - 1.
        int limit=RefundValue+GemValues.Max()-1;
        var plans=new int[]?[limit+1];var counts=Enumerable.Repeat(int.MaxValue,limit+1).ToArray();plans[0]=new int[GemKeys.Length];counts[0]=0;
        for(int g=0;g<GemKeys.Length;g++)for(int item=0;item<Math.Min(inventory[g],limit/GemValues[g]);item++)for(int total=limit;total>=GemValues[g];total--){
            int prior=total-GemValues[g];if(plans[prior] is null||counts[prior]+1>=counts[total])continue;
            plans[total]=(int[])plans[prior]!.Clone();plans[total]![g]++;counts[total]=counts[prior]+1;
        }
        for(int total=RefundValue;total<=limit;total++)if(plans[total] is not null)return plans[total];return null;
    }
    public static LiveBuildSnapshot Parse(string json){
        using var doc=JsonDocument.Parse(json);
        if(doc.RootElement.TryGetProperty("error",out var error))throw new IOException(error.GetString());
        var s=JsonSerializer.Deserialize<LiveBuildSnapshot>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new IOException("未读到角色。");
        if(s.Attributes is not {Length:8}||s.Baseline is not {Length:5}||s.Skills is not {Length:>0}||!Regex.IsMatch(s.Token??"","^[0-9a-f]{16}$")||s.Crowns<0||s.Attributes.Any(x=>x<0)||s.Skills.Select(x=>x.Id).Distinct().Count()!=s.Skills.Length||s.Gems is null||s.Gems.Length!=GemKeys.Length||s.Gems.Any(x=>x<0||x>GemLimit)||s.AttributeReasons is not {Length:5}||s.Credits<0||s.Credits>6||s.Contracts<0||s.Contracts>1000000||s.Ledger!=1000000000+s.Contracts*10+s.Credits)throw new IOException("角色或历练数据不完整，请使用新版助手组件。");
        return s;
    }
    public static void Validate(LiveBuildSnapshot s,PointRefund r){
        if(r.Attribute){if(r.Id<0||r.Id>=5||s.Attributes[r.Id]<=s.Baseline[r.Id])throw new IOException("不能退回角色初始属性。");if(s.AttributeReasons[r.Id].Length>0)throw new IOException(s.AttributeReasons[r.Id]);}
        else{var skill=s.Skills.SingleOrDefault(x=>x.Id==r.Id);if(skill is null||!skill.Learned||skill.Reason.Length>0)throw new IOException("请先退回后续技能；此技能当前不可退回。");}
        if(!s.SafePlace)throw new IOException("请回到城镇或马车营地再退点。");
        if(s.Credits<1)throw new IOException("历练机会不足：完成并交付一个契约可获得 2 次机会。");
        string missing=Missing(s,r.Materials);if(missing.Length>0)throw new IOException("主背包缺少："+missing);
        int value=MaterialValue(r.Materials);if(value<RefundValue)throw new IOException($"所选宝石基础价值 {value}，还差 {RefundValue-value}，需至少 {RefundValue}。");
    }
    public static void Verify(LiveBuildSnapshot before,LiveBuildSnapshot after,PointRefund r){
        Validate(before,r);var expected=(int[])before.Attributes.Clone();expected[r.Attribute?r.Id:6]+=r.Attribute?-1:1;if(r.Attribute)expected[5]++;
        if(before.Character!=after.Character||after.Crowns!=before.Crowns||after.Credits!=before.Credits-1||after.Contracts!=before.Contracts||after.Ledger!=before.Ledger-1||!after.Gems.SequenceEqual(before.Gems.Select((n,i)=>n-r.Materials[i]))||!expected.SequenceEqual(after.Attributes)||before.Skills.Length!=after.Skills.Length||before.Skills.Any(x=>after.Skills.SingleOrDefault(y=>y.Id==x.Id)?.Learned!=(x.Learned&&(r.Attribute||x.Id!=r.Id))))throw new IOException($"退点后的宝石、历练或点数不符合预期。{PointSummary(before,after)}；预期属性点 {expected[5]}、技能点 {expected[6]}。请勿重复退点。");
    }
    public static void VerifySavedMaterials(string path,LiveBuildSnapshot after){
        // Decode the same native slot already verified by BuildEditor.Read.
        string slot=Path.GetFileName(Path.GetDirectoryName(path)!),character=Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)!)!);
        var data=PaidRespec.Decode(File.ReadAllBytes(path),$"stOne!characters_v1!{character}!{slot}!shArd");
        int ledger=checked((int)(data["characterDataMap"]?[LedgerKey]?.GetValue<double>()??-1));
        var counts=new int[GemKeys.Length];foreach(var node in data["inventoryDataList"]!.AsArray()){string key=node![0]!.GetValue<string>();int i=Array.IndexOf(GemKeys,key);if(i>=0)counts[i]++;}
        if(ledger!=after.Ledger||!counts.SequenceEqual(after.Gems))throw new IOException("保存后的材料或历练机会未通过核验。");
    }
}
