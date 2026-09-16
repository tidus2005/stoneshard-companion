using System.Globalization;
using System.Text;
namespace StoneshardCompanion;

public sealed record ForageDefinition(string Key,string Name,string Group,bool Fodder,bool DefaultEnabled=false,bool Peel=false);
public sealed class ForageRule
{
    public bool Enabled {get;set;}
    public int Keep {get;set;}=0;
    public bool SurplusFodder {get;set;}
    public bool AutoPeel {get;set;}
}
public static class ForagePolicy
{
    // Inventory IDs and fodder eligibility checked against the supported EXE's
    // consum_stat_data. Cooked food is deliberately not a collection target.
    public static readonly ForageDefinition[] All=[
        new("o_inv_whortleberry","越橘","浆果",true,true),new("o_inv_raspberry","树莓","浆果",true,true),
        new("o_inv_blueberry","蓝莓","浆果",true,true),new("o_inv_lingonberry","红莓","浆果",true,true),
        new("o_inv_gooseberry","醋栗","浆果",true,true),new("o_inv_barberry","小檗果","浆果",true,true),new("o_inv_grape","葡萄","浆果",true,true),
        new("o_inv_pinecap","松乳菇","蘑菇",false),new("o_inv_pennybun","牛肝菌","蘑菇",false),
        new("o_inv_chanterelle","鸡油菌","蘑菇",false),new("o_inv_morel","羊肚菌","蘑菇",false),
        new("o_inv_stool","毒伞菇","蘑菇",false),new("o_inv_flyagaric","毒蝇伞","蘑菇",false),
        new("o_inv_lentil","小扁豆植株","豆类与食材",true,false,true),new("o_inv_lentils","小扁豆籽","豆类与食材",true),
        new("o_inv_rhubarb","大黄","豆类与食材",true),
        new("o_inv_fleawort","车前草","药草",true,true),new("o_inv_agrimony","龙牙草","药草",true,true),
        new("o_inv_bogbean","睡菜","药草",true,true),new("o_inv_burdock","牛蒡","药草",true,true),
        new("o_inv_mindwort","明智草","药草",true,true),new("o_inv_peppermint","薄荷","药草",true,true),
        new("o_inv_thyme","百里香","药草",true,true),new("o_inv_burnet","地榆","药草",true,true),
        new("o_inv_wormwood","苦艾","药草",true,true),new("o_inv_lavender","薰衣草","药草",true,true),
        new("o_inv_hop","啤酒花","药草",false),new("o_inv_nettle","荨麻","药草",false),
        new("o_inv_henbane","天仙子","药草",false),new("o_inv_horsetail","木贼","药草",false),
        new("o_inv_poppy","罂粟","药草",false),new("o_inv_hemp","大麻","药草",false),
        new("@edible_mushrooms","食用蘑菇合计（四种共用数量）","共享保留组",false),
        new("o_inv_wildegg","鸟蛋（从鸟巢采集）","豆类与食材",false)
    ];
    public static Dictionary<string,ForageRule> Normalize(Dictionary<string,ForageRule>? source)=>All.ToDictionary(d=>d.Key,d=>{
        var r=source?.GetValueOrDefault(d.Key);
        return new ForageRule{Enabled=r?.Enabled??d.DefaultEnabled,Keep=Math.Clamp(r?.Keep??0,0,999),SurplusFodder=d.Fodder&&(r?.SurplusFodder??false),AutoPeel=d.Peel&&(r?.AutoPeel??false)};
    },StringComparer.Ordinal);
    public static string Encode(Dictionary<string,ForageRule> rules){
        // Stable catalog indexes keep every row well inside the existing 2 KiB
        // command payload. Missing/invalid configurations fail closed natively.
        var b=new StringBuilder("F1;");
        for(int i=0;i<All.Length;i++){
            var d=All[i];var r=rules.GetValueOrDefault(d.Key);
            r??=new ForageRule();
            if(r.Keep is <0 or >999)throw new ArgumentException("保留数量须为 0–999");
            int flags=(r.Enabled?4:0)|(d.Fodder&&r.SurplusFodder?1:0)|(d.Peel&&r.AutoPeel?2:0);
            b.Append(CultureInfo.InvariantCulture,$"{i},{r.Keep},{flags};");
        }
        return b.ToString();
    }
}
