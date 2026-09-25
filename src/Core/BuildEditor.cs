using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace StoneshardCompanion;

public sealed record BuildSkill(string Key,bool Learned,bool Fixed);
public sealed record BuildSnapshot(string Character,string Slot,int Level,int Crowns,int AP,int SP,
    int[] Attributes,int[] Baseline,BuildSkill[] Skills,string Fingerprint,string IndexFingerprint,string Path,DateTime SavedAt);
public sealed record BuildAllocation(int[] Attributes,string[] Skills);
public sealed record BuildEditResult(string Backup,string Path,int Crowns,int AP,int SP);

// The draft never writes. A single validated replacement commits both allocation
// and fee. Live game memory is deliberately not mixed with an edited disk save.
public static class BuildEditor
{
    public const int Price=500;
    public static readonly string[] AttributeLabels=["力量","敏捷","感知","活力","意志"];
    public static int Int(JsonNode? value){
        if(value is not JsonValue v||!double.TryParse(v.ToJsonString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n)||!double.IsFinite(n)||n<0||n>1_000_000||n!=Math.Truncate(n))throw new IOException("加点存档数值无效。");
        return (int)n;
    }
    private static bool Flag(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<bool>(out var b)?b:Int(n)!=0;
    private static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
    private static string Salt(string character,string slot)=>$"stOne!characters_v1!{character}!{slot}!shArd";
    private static (string Path,string Character,string Slot,byte[] Index) Locate(string root){
        root=SavePaths.ResolveDirectoryRoot(root);
        var indexPath=System.IO.Path.Combine(root,"characters_v1","characters.map");SavePaths.AssertNotLink(indexPath);
        var index=File.ReadAllBytes(indexPath);var map=PaidRespec.Decode(index,"stOne!characters_v1!shArd");
        string character=map["lastCharacter"]!.GetValue<string>(),slot=map["lastSave"]!.GetValue<string>();
        if(!Regex.IsMatch(character,"^character_[0-9]+$")||!Regex.IsMatch(slot,"^(save_[1-3]|autosave_[1-3]|exitsave_1)$"))throw new IOException("最后使用的角色或存档槽位不受支持。");
        string path=System.IO.Path.Combine(root,"characters_v1",character,slot,"data.sav");
        for(string? p=path;p is not null&&!p.Equals(root,StringComparison.OrdinalIgnoreCase);p=System.IO.Path.GetDirectoryName(p))SavePaths.AssertNotLink(p);
        string metadataPath=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!,"save.map");SavePaths.AssertNotLink(metadataPath);
        var metadata=PaidRespec.Decode(File.ReadAllBytes(metadataPath),Salt(character,slot));
        if(!Flag(metadata["valid"]))throw new IOException("这个槽位已被游戏标为无效或已使用。请先正常保存退出，或还原保险备份，再刷新。");
        return(path,character,slot,index);
    }
    public static BuildSnapshot Read(string root){
        var loc=Locate(root);byte[] raw=File.ReadAllBytes(loc.Path);var data=PaidRespec.Decode(raw,Salt(loc.Character,loc.Slot));
        var c=data["characterDataMap"]!.AsObject();var game=data["gameDataMap"]!;
        if(game["wipeVersion"]!.GetValue<string>()!="0.9"||game["compiler"]!.GetValue<string>()!="YYC"||Flag(game["prologue"]))throw new IOException("仅支持当前 0.9 冒险模式存档。");
        string name=c["nameKey"]!.GetValue<string>();
        if(!PaidRespec.Bases.TryGetValue(name,out var baseline))throw new IOException("尚未核实这个角色的初始属性："+name);
        int[] attributes=PaidRespec.Attributes.Select(k=>Int(c[k])).ToArray();int level=Int(c["LVL"]);
        if(level is <1 or >30||attributes.Where((n,i)=>n<baseline[i]||n>30).Any())throw new IOException("角色等级或基础属性超出支持范围。");
        var all=data["skillsDataMap"]!["skillsAllDataList"]!.AsArray();
        if(all.Count==0||all.Count%5!=0)throw new IOException("技能存档结构异常。");
        var skills=new List<BuildSkill>();var seen=new HashSet<string>(StringComparer.Ordinal);
        for(int i=0;i<all.Count;i+=5){string key=all[i]!.GetValue<string>();
            if(!seen.Add(key)||!Regex.IsMatch(key,"^o_(pass_)?skill_[A-Za-z0-9_]+$"))throw new IOException("发现未知或重复的技能记录。");
            skills.Add(new(key,Flag(all[i+1]),PaidRespec.FreeSkills.Contains(key)));
        }
        int gold=0;foreach(var entry in data["inventoryDataList"]!.AsArray()){
            var row=entry!.AsArray();if(row.Count!=10)throw new IOException("物品存档结构异常。");
            if(row[0]!.GetValue<string>() is "o_inv_gold" or "o_inv_moneybag")gold=checked(gold+Int(row[1]!["Stack"]));
        }
        return new(name,loc.Character+"/"+loc.Slot,level,gold,Int(c["AP"]),Int(c["SP"]),attributes,[..baseline],skills.ToArray(),Hash(raw),Hash(loc.Index),loc.Path,File.GetLastWriteTime(loc.Path));
    }
    public static BuildAllocation Draft(BuildSnapshot s)=>new([..s.Attributes],s.Skills.Where(x=>x.Learned).Select(x=>x.Key).ToArray());
    public static (int AP,int SP,bool Changed) Validate(BuildSnapshot s,BuildAllocation draft){
        if(draft.Attributes.Length!=5||draft.Attributes.Where((n,i)=>n<s.Baseline[i]||n>30).Any())throw new IOException("属性必须介于角色初始值与 30 之间。");
        var selected=draft.Skills.ToHashSet(StringComparer.Ordinal);var known=s.Skills.Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
        if(selected.Count!=draft.Skills.Length||!selected.IsSubsetOf(known)||s.Skills.Any(x=>x.Fixed&&selected.Contains(x.Key)!=x.Learned))throw new IOException("基础动作不可增删，不能写入未知或重复技能。");
        int ap=checked(s.AP+s.Attributes.Sum()-draft.Attributes.Sum());
        int sp=checked(s.SP+s.Skills.Count(x=>x.Learned&&!x.Fixed)-s.Skills.Count(x=>selected.Contains(x.Key)&&!x.Fixed));
        if(ap<0||sp<0)throw new IOException("剩余点数不足，属性点与技能点不能互换。");
        bool changed=!s.Attributes.SequenceEqual(draft.Attributes)||s.Skills.Any(x=>x.Learned!=selected.Contains(x.Key));
        return(ap,sp,changed);
    }
    private static JsonObject Transform(JsonObject data,BuildSnapshot s,BuildAllocation draft){
        var balance=Validate(s,draft);if(!balance.Changed)throw new IOException("加点没有变化，不需要付费。");
        if(s.Crowns<Price)throw new IOException($"随身克朗不足：{s.Crowns} / {Price}。");
        var c=data["characterDataMap"]!;for(int i=0;i<5;i++)c[PaidRespec.Attributes[i]]=draft.Attributes[i];c["AP"]=balance.AP;c["SP"]=balance.SP;
        var chosen=draft.Skills.ToHashSet(StringComparer.Ordinal);var removed=s.Skills.Where(x=>x.Learned&&!chosen.Contains(x.Key)).Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
        var skills=data["skillsDataMap"]!;var all=skills["skillsAllDataList"]!.AsArray();
        for(int i=0;i<all.Count;i+=5){string key=all[i]!.GetValue<string>();if(Flag(all[i+1])!=chosen.Contains(key)){all[i+1]=chosen.Contains(key)?1:0;all[i+2]=0;}}
        foreach(var rowNode in skills["skillsPanelDataList"]!.AsArray()){
            var row=rowNode!.AsArray();for(int i=0;i<row.Count;i++)if(row[i] is JsonValue v&&v.TryGetValue<string>(out var action)&&(removed.Contains(action)||removed.Contains(action+"_ico")))row[i]=-4;
        }
        var buffs=c["buffs"]!.AsArray();if(buffs.Count%8!=0)throw new IOException("状态记录结构异常。");
        for(int i=buffs.Count-8;i>=0;i-=8){string obj=buffs[i]!.GetValue<string>();string? origin=buffs[i+6] is JsonValue val&&val.TryGetValue<string>(out var text)?text:null;
            if((origin is not null&&(removed.Contains(origin)||removed.Contains(origin+"_ico")))||removed.Any(k=>obj==k.Replace("o_pass_skill_","o_b_",StringComparison.Ordinal).Replace("o_skill_","o_b_",StringComparison.Ordinal).Replace("_ico","",StringComparison.Ordinal)))for(int j=0;j<8;j++)buffs.RemoveAt(i);
        }
        var inventory=data["inventoryDataList"]!.AsArray();int remaining=Price;
        foreach(var entry in inventory.ToArray()){
            var row=entry!.AsArray();string key=row[0]!.GetValue<string>();if(key is not ("o_inv_gold" or "o_inv_moneybag"))continue;
            var item=row[1]!.AsObject();int before=Int(item["Stack"]),take=Math.Min(before,remaining);item["Stack"]=before-take;remaining-=take;
            if(take==before){if(key=="o_inv_gold")inventory.Remove(row);else if(item.ContainsKey("i_index"))item["i_index"]=0;}
            if(remaining==0)break;
        }
        if(remaining!=0)throw new IOException("扣款核对失败，未写入存档。");return data;
    }
    public static BuildEditResult Apply(string root,BuildSnapshot accepted,BuildAllocation draft,Func<string> backup){
        PaidRespec.RequireGameClosed();var current=Read(root);
        void Fresh(){PaidRespec.RequireGameClosed();var now=Read(root);if(now.Fingerprint!=accepted.Fingerprint||now.IndexFingerprint!=accepted.IndexFingerprint||now.Path!=accepted.Path)throw new IOException("存档已经变化，请刷新并重新调整；未扣款。");}
        Fresh();var loc=Locate(root);string salt=Salt(loc.Character,loc.Slot);
        var result=Transform(PaidRespec.Decode(File.ReadAllBytes(current.Path),salt),current,draft);byte[] output=PaidRespec.Encode(result,salt);
        if(!JsonNode.DeepEquals(PaidRespec.Decode(output,salt),result))throw new IOException("编辑结果校验失败。");
        string archive=backup();Fresh();string temporary=current.Path+".build-"+Guid.NewGuid().ToString("N")+".tmp";
        try{
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(output);file.Flush(true);}
            Fresh();File.Move(temporary,current.Path,true);
            if(Hash(File.ReadAllBytes(current.Path))!=Hash(output))throw new IOException("写入校验失败，请还原保险备份："+archive);
        }finally{if(File.Exists(temporary))File.Delete(temporary);}
        var balance=Validate(current,draft);return new(archive,current.Path,current.Crowns-Price,balance.AP,balance.SP);
    }
}
