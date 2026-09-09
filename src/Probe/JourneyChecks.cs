using StoneshardCompanion;
internal static class JourneyChecks
{
    public static void Run(){int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
        Provision Food(double uses=1,double delta=-14,double? fresh=null)=>new("肉串",uses,delta,null,fresh,false);
        Check(JourneyEstimator.Hours(15,5,[],false)==2,"current need counted before supplies");
        Check(JourneyEstimator.Hours(25,5,[Food(2)],false)==5.6,"food uses and per-hour units");
        Check(JourneyEstimator.Hours(25,5,[Food(1,-80)],false)==5,"excess food restoration capped at 25");
        Check(JourneyEstimator.Hours(50,5,[Food(1)],false)==0,"already excessive hunger needs repayment");
        Check(JourneyEstimator.Hours(25,5,[Food(100,-25,1)],false)==5,"only food eaten before expiry counts");
        Check(JourneyEstimator.Hours(15,5,[Food(2,-25,2)],false)==2,"food expired at first meal is excluded");
        Check(JourneyEstimator.Hours(25,5,[Food(2,-25,0)],false)==0,"already rotten food excluded");
        Check(JourneyEstimator.Hours(25,5,[new("未知",1,null,null,null,false)],false)==0,"unknown restoration never fabricated");
        Check(JourneyEstimator.Hours(25,null,[Food()],false) is null,"unknown consumption means unknown endurance");
        Check(JourneyEstimator.Hours(25,5,[new("水",3,null,-20,null,true)],true)==12,"water uses calculated separately");
        Check(JourneyEstimator.Severity(new("坏",0,100),25)==2&&JourneyEstimator.Severity(new("低",25,100),25)==1&&JourneyEstimator.Severity(new("好",26,100),25)==0,"broken red, threshold yellow, healthy normal");
        var e=new JourneyEstimator();long at=Environment.TickCount64;
        CharacterTelemetry Sample(int ago,double minute)=>new(){At=at-ago,Player=1,Journey=new(){Minutes=minute,Complete=true,Provisions=[Food()]}};
        Check(e.Update(Sample(2,100),10,10).FoodHours is null,"rate needs initial observations");
        e.Update(Sample(2,100),10.49,10.99);
        var measured=e.Update(Sample(1,106),10.5,11);
        Check(Math.Abs(measured.HungerPerHour!.Value-5)<.0001&&Math.Abs(measured.ThirstPerHour!.Value-10)<.0001,"rates measured from game minutes, independent of wall clock speed");
        Check(e.Update(Sample(0,107),0,0).HungerPerHour==5,"eating excluded from natural need rate");
        var newMap=Sample(0,150);newMap.Player=2;
        Check(e.Update(newMap,25,25).HungerPerHour==5,"map instance replacement preserves learned rate and ignores transition need jump");
        Check(new JourneyEstimator().Update(Sample(0,100),25,25,4,8).FoodHours==3.5,"explicit hourly rate supported before calibration");
        Check(e.Update(new(),10,10).FoodHours is null,"stale telemetry hides estimates");
        Check(CharacterTelemetry.Parse("{\"journey\":{\"equipment\":[{\"name\":\"坏数据\",\"current\":1,\"maximum\":0}]}}").At==0,"invalid equipment denominator rejected");
        var stats=new CharacterTelemetry{Stats=[new("STR",22,null),new("Weapon_Damage",118,100)]};
        Check(StatMechanics.Contributions(stats,"Weapon_Damage").Single().Delta==18&&StatMechanics.WeaponEquation(stats).Contains("118%"),"verified strength equation matches current character");
        Check(StatMechanics.Contributions(stats,"CRTD").Single().Delta==20,"strength milestones count completed thresholds");
        Check(StatMechanics.CleanLabel("~y~疲劳~/~")=="疲劳","game color markup removed from labels");
        Console.WriteLine($"{passed} journey and mechanics checks passed. No game/UI accessed.");
    }
}
