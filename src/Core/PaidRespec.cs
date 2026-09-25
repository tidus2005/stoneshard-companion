using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace StoneshardCompanion;

public sealed record RespecPreview(string Character,string Slot,int Level,int Crowns,int RefundedAttributes,int RefundedSkills,int AvailableAttributes,int AvailableSkills,string Fingerprint,string IndexFingerprint,string Path,string Summary);
public sealed record RespecResult(string Backup,int CrownsRemaining,int AttributePoints,int SkillPoints);
public static class PaidRespec
{
    public const int Price=2000;
    internal static readonly string[] Attributes=["STR","AGL","PRC","Vitality","WIL"];
    internal static readonly Dictionary<string,int[]> Bases=new(StringComparer.Ordinal){
        ["Arna"]=[11,11,10,11,10],["Jorgrim"]=[11,10,11,11,10],["Dirwin"]=[10,11,11,11,10],
        ["Jonna"]=[10,10,11,11,11],["Velmir"]=[11,11,11,10,10]
    };
    // Basic actions cost no points. Torch Strike is supplied by equipment.
    internal static readonly HashSet<string> FreeSkills=["o_skill_butchering_ico","o_skill_craft_ico","o_pass_skill_Sudden_Attacks","o_skill_trap_search_ico","o_skill_torch_strike_ico"];
    public static bool GameRunning(){var games=Process.GetProcessesByName("StoneShard");try{return games.Length!=0;}finally{foreach(var game in games)game.Dispose();}}
    public static void RequireGameClosed(){if(GameRunning())throw new IOException("请先在游戏中保存并退出到桌面，再进行洗点；不会强制结束游戏。");}
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static int Integer(JsonNode? value,string field){
        if(value is not JsonValue v||!double.TryParse(v.ToJsonString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n)||!double.IsFinite(n)||n!=Math.Truncate(n)||n<0||n>1_000_000)throw new IOException("存档数值不受支持："+field);
        return (int)n;
    }
    private static bool Flag(JsonNode? n){if(n is JsonValue v&&v.TryGetValue<bool>(out bool b))return b;return Integer(n,"技能状态")!=0;}
    private static JsonObject Object(JsonNode? n,string field)=>n as JsonObject??throw new IOException("缺少存档结构："+field);
    private static JsonArray Array(JsonNode? n,string field)=>n as JsonArray??throw new IOException("缺少存档列表："+field);
    private static string Text(JsonNode? n)=>n?.GetValue<string>()??throw new IOException("缺少存档文字字段。");
    public static JsonObject Decode(byte[] bytes,string? salt){
        if(bytes.Length>64_000_000)throw new IOException("存档过大，停止洗点。");
        using var source=new MemoryStream(bytes);using var z=new ZLibStream(source,CompressionMode.Decompress);using var output=new MemoryStream();
        var block=new byte[65536];int read;while((read=z.Read(block))>0){if(output.Length+read>128_000_000)throw new IOException("存档解压超出上限。");output.Write(block,0,read);}
        var data=output.ToArray();if(data.Length<34||data[^1]!=0)throw new IOException("存档尾部校验格式错误。");
        var body=data[..^33];string digest=Encoding.ASCII.GetString(data[^33..^1]);
        if(!Regex.IsMatch(digest,"^[0-9a-f]{32}$"))throw new IOException("存档校验格式错误。");
        if(salt is not null){string expected=Convert.ToHexString(MD5.HashData([..body,..Encoding.UTF8.GetBytes(salt)])).ToLowerInvariant();if(expected!=digest)throw new IOException("存档校验不匹配，未修改任何数据。");}
        return JsonNode.Parse(body,documentOptions:new(){MaxDepth=128}) as JsonObject??throw new IOException("存档格式错误。");
    }
    // GameMaker saves nested JSON maps as strings. Keep UTF-8 and escaped quotes
    // in their native form; HTML-safe Unicode escaping fails the native reader.
    public static byte[] Encode(JsonObject data,string salt){
        byte[] json=Encoding.UTF8.GetBytes(data.ToJsonString(new System.Text.Json.JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping})),digest=Encoding.ASCII.GetBytes(Convert.ToHexString(MD5.HashData([..json,..Encoding.UTF8.GetBytes(salt)])).ToLowerInvariant());
        using var output=new MemoryStream();using(var z=new ZLibStream(output,CompressionLevel.Optimal,true)){z.Write(json);z.Write(digest);z.WriteByte(0);}return output.ToArray();
    }
    private static string Salt(string character,string slot)=>$"stOne!characters_v1!{character}!{slot}!shArd";
    private static (string Path,string Character,string Slot,byte[] Index) Locate(string saveRoot){
        string root=SavePaths.ResolveDirectoryRoot(saveRoot),index=System.IO.Path.Combine(root,"characters_v1","characters.map");SavePaths.AssertNotLink(index);
        byte[] indexBytes=File.ReadAllBytes(index);var map=Decode(indexBytes,"stOne!characters_v1!shArd");
        string character=Text(map["lastCharacter"]),slot=Text(map["lastSave"]);
        if(!Regex.IsMatch(character,"^character_[0-9]+$")||slot!="exitsave_1")throw new IOException("请先保存并退出游戏，让最后使用的存档成为退出存档，再刷新预览。");
        string path=System.IO.Path.Combine(root,"characters_v1",character,slot,"data.sav");
        for(string? p=path;p is not null&&!p.Equals(root,StringComparison.OrdinalIgnoreCase);p=System.IO.Path.GetDirectoryName(p))SavePaths.AssertNotLink(p);
        return(path,character,slot,indexBytes);
    }
    private static (JsonObject Result,int Gold,int AP,int SP,int RefundAP,int RefundSP,string Name,int Level) Plan(JsonObject original){
        var data=(JsonObject)original.DeepClone();var c=Object(data["characterDataMap"],"characterDataMap");var game=Object(data["gameDataMap"],"gameDataMap");
        if(Text(game["wipeVersion"])!="0.9"||Text(game["compiler"])!="YYC"||Flag(game["prologue"]))throw new IOException("暂不支持这个存档版本或序章角色。");
        string name=Text(c["nameKey"]);if(!Bases.TryGetValue(name,out var baseline))throw new IOException("尚未核实该角色的初始属性，暂不支持洗点："+name);
        int level=Integer(c["LVL"],"LVL"),ap=Integer(c["AP"],"AP"),sp=Integer(c["SP"],"SP"),refundAP=0;
        if(level is <1 or >30)throw new IOException("角色等级超出已支持范围。");
        for(int i=0;i<Attributes.Length;i++){int current=Integer(c[Attributes[i]],Attributes[i]);if(current<baseline[i]||current>30)throw new IOException("属性超出角色正常范围，停止洗点。");refundAP+=current-baseline[i];c[Attributes[i]]=baseline[i];}
        var skills=Object(data["skillsDataMap"],"skillsDataMap");var all=Array(skills["skillsAllDataList"],"skillsAllDataList");
        if(all.Count==0||all.Count%5!=0)throw new IOException("技能记录格式不受支持。");
        var removed=new HashSet<string>(StringComparer.Ordinal);var seen=new HashSet<string>(StringComparer.Ordinal);
        for(int i=0;i<all.Count;i+=5){string key=Text(all[i]);if(!seen.Add(key))throw new IOException("技能记录重复。");if(Flag(all[i+1])&&!FreeSkills.Contains(key)){if(!key.StartsWith("o_skill_",StringComparison.Ordinal)&&!key.StartsWith("o_pass_skill_",StringComparison.Ordinal))throw new IOException("无法识别已学习的技能："+key);removed.Add(key);all[i+1]=false;all[i+2]=0;}}
        if(refundAP==0&&removed.Count==0)throw new IOException("没有已分配的属性点或技能点，无需付费洗点。");
        c["AP"]=checked(ap+refundAP);c["SP"]=checked(sp+removed.Count);
        var panel=Array(skills["skillsPanelDataList"],"skillsPanelDataList");
        foreach(var rowNode in panel){var row=Array(rowNode,"技能栏");for(int i=0;i<row.Count;i++)if(row[i] is JsonValue v&&v.TryGetValue<string>(out string? action)&&removed.Contains(action+"_ico"))row[i]=-4;}
        // Timed passive modifiers carry their skill in source[6]. Remove those,
        // while preserving injuries, food, caravan and item effects.
        var buffs=Array(c["buffs"],"buffs");if(buffs.Count%8!=0)throw new IOException("状态记录格式不受支持。");
        for(int i=buffs.Count-8;i>=0;i-=8){string obj=Text(buffs[i]);string? origin=buffs[i+6] is JsonValue val&&val.TryGetValue<string>(out var s)?s:null;
            bool fromSkill=origin is not null&&(removed.Contains(origin)||removed.Contains(origin+"_ico"));
            bool namedSkill=removed.Any(k=>obj==k.Replace("o_pass_skill_","o_b_",StringComparison.Ordinal).Replace("o_skill_","o_b_",StringComparison.Ordinal).Replace("_ico","",StringComparison.Ordinal));
            if(fromSkill||namedSkill)for(int j=0;j<8;j++)buffs.RemoveAt(i);
        }
        var inventory=Array(data["inventoryDataList"],"inventoryDataList");var purses=new List<(JsonObject Item,JsonArray Entry,string Key)>();int gold=0;
        foreach(var entryNode in inventory){var entry=Array(entryNode,"物品");if(entry.Count!=10)throw new IOException("物品记录格式不受支持。");string key=Text(entry[0]);
            if(key!="o_inv_moneybag"&&key!="o_inv_gold")continue;
            var item=Object(entry[1],"钱袋");int stack=Integer(item["Stack"],"克朗");gold=checked(gold+stack);purses.Add((item,entry,key));
        }
        if(gold<Price)throw new IOException($"主物品栏中的克朗不足：{gold} / {Price}。请先取出钱款再保存退出。");
        int remaining=Price;foreach(var purse in purses){int before=Integer(purse.Item["Stack"],"克朗"),take=Math.Min(before,remaining);purse.Item["Stack"]=before-take;remaining-=take;if(before==take){if(purse.Key=="o_inv_gold")inventory.Remove(purse.Entry);else if(purse.Item.ContainsKey("i_index"))purse.Item["i_index"]=0;}if(remaining==0)break;}
        return(data,gold,ap+refundAP,sp+removed.Count,refundAP,removed.Count,name,level);
    }
    public static RespecPreview Preview(string root){
        var located=Locate(root);byte[] raw=File.ReadAllBytes(located.Path);var p=Plan(Decode(raw,Salt(located.Character,located.Slot)));
        string summary=string.Join("、",new[]{"力量","敏捷","感知","活力","意志"}.Zip(Bases[p.Name],(key,n)=>$"{key} {n}"));
        return new(p.Name,located.Character+"/"+located.Slot,p.Level,p.Gold,p.RefundAP,p.RefundSP,p.AP,p.SP,Hash(raw),Hash(located.Index),located.Path,summary);
    }
    // Called while SaveEngine holds the same manager lock as backup/restore.
    public static RespecResult Apply(string root,RespecPreview accepted,Func<string> backup){
        RequireGameClosed();var current=Preview(root);
        if(current!=accepted)throw new IOException("存档或预览已改变，请刷新后重新确认。未扣款。");
        byte[] before=File.ReadAllBytes(current.Path);var located=Locate(root);string salt=Salt(located.Character,located.Slot);var plan=Plan(Decode(before,salt));byte[] output=Encode(plan.Result,salt);
        if(!JsonNode.DeepEquals(Decode(output,salt),plan.Result))throw new IOException("洗点结果校验失败。未扣款。");
        string archive=backup();RequireGameClosed();if(Preview(root)!=accepted)throw new IOException("备份期间存档已改变，未扣款。");
        string temporary=current.Path+".respec-"+Guid.NewGuid().ToString("N")+".tmp";
        try{
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(output);file.Flush(true);}
            RequireGameClosed();
            using var originalGuard=new FileStream(current.Path,FileMode.Open,FileAccess.Read,FileShare.Delete);
            if(Convert.ToHexString(SHA256.HashData(originalGuard))!=current.Fingerprint)throw new IOException("存档已改变，未扣款。");
            if(Hash(File.ReadAllBytes(System.IO.Path.Combine(root,"characters_v1","characters.map")))!=current.IndexFingerprint)throw new IOException("选中的角色已改变，未扣款。");
            originalGuard.Dispose(); // Windows replacement requires the destination handle closed.
            RequireGameClosed();File.Move(temporary,current.Path,true);
            if(Hash(File.ReadAllBytes(current.Path))!=Hash(output))throw new IOException("写入后的校验失败，请从操作前保险备份还原："+archive);
        }finally{if(File.Exists(temporary))File.Delete(temporary);}
        return new(archive,plan.Gold-Price,plan.AP,plan.SP);
    }
}
