using System.Text.Json;
namespace StoneshardCompanion;
public static class CombatPreviewChecks
{
    public static void Run(){
        int checks=0;void Check(bool v,string label){if(!v)throw new Exception(label);checks++;}
        Dictionary<string,double> A(double hit,double fumble)=>new(){["Hit_Chance"]=hit,["FMB"]=fumble,["is_range"]=0};
        Dictionary<string,double> D(double dodge)=>new(){["EVS"]=dodge,["is_range"]=0,["is_mage"]=0};
        var f=CombatPreview.Melee(A(90,10),D(20))!;
        Check(Math.Abs(f.Clean-64.8)<1e-8&&Math.Abs(f.Graze-23.4)<1e-8&&Math.Abs(f.Miss-11.8)<1e-8,"Separate rolls and dodge downgrade");
        f=CombatPreview.Melee(A(120,0),D(20))!;Check(f.Clean==100&&f.Miss==0,"Excess accuracy offsets dodge");
        f=CombatPreview.Melee(A(80,0),D(-20))!;Check(f.AccuracyRoll==100,"Negative dodge improves accuracy");
        Check(CombatPreview.Melee(new Dictionary<string,double>(),D(0)) is null,"Unknown is not zero");
        foreach(double a in new[]{-50d,0,50,100,150})foreach(double d in new[]{-50d,0,50,100,150})foreach(double fm in new[]{-5d,0,50,100,120}){
            f=CombatPreview.Melee(A(a,fm),D(d))!;Check(Math.Abs(f.Clean+f.Graze+f.Miss-100)<1e-8&&Math.Min(f.Clean,Math.Min(f.Graze,f.Miss))>=-1e-8,"Probability mass conserved at extremes");
        }
        var s=new CombatTelemetry{At=1000,Scene=3,TargetId=123,Name="目标",Distance=1,Player=A(90,10),Target=D(20)};
        Check(CombatTelemetry.Parse(JsonSerializer.Serialize(s)).Fresh(1200,3),"Valid snapshot");
        Check(!s.Fresh(1500,3)&&!s.Fresh(900,3)&&!s.Fresh(1200,4),"Stale, future or wrong-scene rejected");
        foreach(string bad in new[]{"{","null","{}","{\"targetId\":1,\"distance\":1,\"target\":null}",new string('x',8193)})Check(CombatTelemetry.Parse(bad).At==0,"Corrupt snapshot rejected");
        s.SkillSelected=true;var rows=CombatPreview.Describe(s,new(),false);Check(rows[0].Rows.All(r=>r.Label!="正常或暴击命中"),"Unsupported skill never inherits basic attack odds");
        foreach(var mouse in new[]{(-1900d,-500d),(-100d,100d),(1800d,1000d)}){
            var p=CombatPreview.Place(mouse.Item1,mouse.Item2,900,600,-1920,-600,0,480);Check(p.X>=-1920&&p.X+900<=0&&p.Y>=-600&&p.Y+600<=480,"Negative monitor edges remain contained");
        }
        var bytes=new byte[EngineBridge.SnapshotSize];var decoded=EngineBridge.DecodeSnapshot(bytes);Check(decoded.Combat.At==0&&!decoded.CombatEnabled,"Empty bridge payload hides preview");
        Console.WriteLine($"{checks} combat preview checks passed; no game/save access.");
    }
}
