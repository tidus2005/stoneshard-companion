using StoneshardCompanion;
using System.Text;
internal static class CharacterChecks
{
    public static void Run(){int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
        var data=CharacterTelemetry.Parse("""{"at":5000,"player":7,"stats":[{"key":"Weapon_Damage","value":112.5,"baseline":100},{"key":"STR","value":20,"baseline":null}],"sources":[{"key":"Weapon_Damage","delta":7.5,"sourceId":42,"label":"效果 A"},{"key":"Magic_Power","delta":25,"sourceId":8,"label":"法术效果"}],"sourcesAvailable":true,"foods":[{"key":"o_inv_berry","name":"浆果","value":4}]}""");
        Check(data.Fresh(7999)&&!data.Fresh(8000)&&!data.Fresh(4999),"expiry and future timestamps reject stale player attributes");
        var weapon=StatsCatalog.All.Single(x=>x.Key=="Weapon_Damage");var explanation=data.Explain(weapon);
        Check(explanation.Contains("112.5%")&&explanation.Contains("100%")&&explanation.Contains("+12.5 个百分点"),"native baseline and final delta use percentage points");
        Check(explanation.Contains("效果 A")&&!explanation.Contains("法术效果"),"source details belong only to exact matching stat");
        Check(data.Explain(StatsCatalog.All.Single(x=>x.Key=="STR")).Contains("基线尚不可用"),"missing baseline is not inferred from an observed value");
        Check(CharacterTelemetry.Parse("{").Stats.Length==0&&CharacterTelemetry.Parse("{\"stats\":null}").Stats.Length==0,"malformed or missing arrays fail closed");
        Check(CharacterTelemetry.Parse("{\"foods\":[{\"key\":\"bad|key\",\"value\":1}]}").Foods.Length==0,"malformed material identities cannot enter a command");
        Check(StatsCatalog.All.Select(x=>x.Key).Distinct().Count()==StatsCatalog.All.Length,"catalogue stable keys are unique across categories");
        Check(StatsCatalog.All.Any(x=>x.Group=="法术")&&StatsCatalog.All.Any(x=>x.Group=="近战与远程"),"caster and melee categories can be filtered independently");
        Check(FodderPolicy.Selection(["berry","herb","berry"])=="|berry||herb|","material command encodes an exact deduplicated allowlist");
        try{FodderPolicy.Selection(["berry|herb"]);throw new Exception("delimiter accepted");}catch(ArgumentException){passed++;}
        try{FodderPolicy.Selection(Enumerable.Range(0,40).Select(i=>"item"+i+new string('x',100)));throw new Exception("oversize accepted");}catch(ArgumentException){passed++;}
        var packet=new byte[EngineBridge.SnapshotSize];Encoding.UTF8.GetBytes("{\"at\":5000}").CopyTo(packet,3968);packet[3968+20]=123;
        Check(EngineBridge.DecodeSnapshot(packet).Telemetry.At==5000,"snapshot ignores bytes after first JSON terminator");
        Check(CharacterTelemetry.FormatComparison(112.5,100,"%")=="112.5%（+12.5 pp）"&&CharacterTelemetry.FormatComparison(8,10,"")=="8（-2）","current and signed delta use correct units");
        Check(CharacterTelemetry.FormatComparison(12,null,"")=="12（—）"&&CharacterTelemetry.FormatComparison(null,10,"")=="—","unknown baseline and stale values are never fabricated");
        Check(CharacterTelemetry.Parse("{\"foods\":[{\"key\":\"o_inv_morel\",\"name\":\"蘑菇\",\"value\":0,\"quantity\":3}]}").Foods.Single().Quantity==3,"non-fodder materials carry true inventory quantities");
        Check(ForagePolicy.Normalize(null).Values.All(r=>r.Keep==0),"all initial reserves are zero");
        var rules=ForagePolicy.Normalize(null);rules["o_inv_lentil"].Enabled=true;rules["o_inv_lentil"].AutoPeel=true;rules["o_inv_lentils"].Keep=9;
        string policy=ForagePolicy.Encode(rules);Check(policy.Contains("13,0,6;")&&policy.Contains("14,9,0;"),"disabled seed row still carries its reserve for automatic peeling");
        Check(Encoding.UTF8.GetByteCount(policy)<2048&&ForagePolicy.All.Select(d=>d.Key).Distinct().Count()==34,"full rules fit protocol and unique catalog");
        rules["o_inv_morel"].SurplusFodder=true;rules["o_inv_morel"].AutoPeel=true;rules=ForagePolicy.Normalize(rules);Check(!rules["o_inv_morel"].SurplusFodder&&!rules["o_inv_morel"].AutoPeel,"unsupported destructive options are sanitized");
        Check(new SaveArchive("x","Stoneshard-manual-test.zip",DateTime.Now,1,false,false).Kind=="手动保留备份","manual backups have an explicit origin");
        Check(new SaveArchive("x","Stoneshard-current-state-test.zip",DateTime.Now,1,false,false).Kind=="当前状态快照","current snapshots are not labeled manual backups");
        Check(StatPresentation.Benefit("STR",2)==1&&StatPresentation.Benefit("STR",-2)==-1,"attribute increases improve and decreases worsen");
        Check(StatPresentation.Benefit("FMB",-2)==1&&StatPresentation.Benefit("Abilities_Energy_Cost",-2)==1&&StatPresentation.Benefit("Cooldown_Reduction",2)==-1,"cost and failure modifiers use inverse benefit direction");
        Check(StatPresentation.Benefit("STR",null)==0&&StatPresentation.Benefit("STR",.001)==0&&StatPresentation.Benefit("unknown",2)==0,"unknown and rounded-zero deltas remain neutral");
        Console.WriteLine($"{passed} attribute and fodder policy checks passed. No game/UI accessed.");
    }
}
