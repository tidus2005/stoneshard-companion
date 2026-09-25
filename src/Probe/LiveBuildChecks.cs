using StoneshardCompanion;
using System.Text.Json;
internal static class LiveBuildChecks
{
    public static void Run(){
        int checks=0;void Check(bool ok){checks++;if(!ok)throw new Exception("Gem refund check "+checks);}
        void Reject(Action a){bool rejected=false;try{a();}catch(IOException){rejected=true;}Check(rejected);}
        var s=new LiveBuildSnapshot("Jorgrim","0123456789abcdef",376,[25,11,15,17,10,0,0,24],[11,10,11,11,10],[new(1,"o_skill_a",0,true,"o_skill_b"),new(2,"o_skill_b",0,true,"")],2,37,1000000372,[2,1,2,2,1,1,1,1],true,"CaravanCamp",["","","","",""]);
        var attr=new PointRefund(true,0,2);var skill=new PointRefund(false,2,4);
        LiveBuild.Validate(s,attr);LiveBuild.Validate(s,skill);Check(true);
        Reject(()=>LiveBuild.Validate(s,new(false,1,4)));Reject(()=>LiveBuild.Validate(s,new(true,4,2)));Reject(()=>LiveBuild.Validate(s,new(true,5,2)));Reject(()=>LiveBuild.Validate(s,new(false,999,4)));
        Reject(()=>LiveBuild.Validate(s with{Credits=0},attr));Reject(()=>LiveBuild.Validate(s with{SafePlace=false},skill));
        Reject(()=>LiveBuild.Validate(s,attr with{Recipe=4}));Reject(()=>LiveBuild.Validate(s,skill with{Recipe=2}));Reject(()=>LiveBuild.Validate(s,skill with{Recipe=100}));
        Reject(()=>LiveBuild.Validate(s with{Gems=new int[8]},attr));Reject(()=>LiveBuild.Validate(s with{AttributeReasons=["dependency","","","",""]},attr));
        LiveBuild.Validate(s with{Crowns=0},attr);Check(true);
        foreach(var r in LiveBuild.Recipes){var request=new PointRefund(r.Attribute??true,r.Attribute==false?2:0,r.Id);LiveBuild.Validate(s,request);Check(true);}
        var refunded=s with{Credits=1,Ledger=s.Ledger-1,Gems=[2,1,2,2,0,1,1,1],Attributes=[24,11,15,17,10,1,0,24]};LiveBuild.Verify(s,refunded,attr);Check(true);
        Reject(()=>LiveBuild.Verify(s,refunded with{Crowns=375},attr));Reject(()=>LiveBuild.Verify(s,refunded with{Gems=s.Gems},attr));Reject(()=>LiveBuild.Verify(s,refunded with{Credits=2},attr));Reject(()=>LiveBuild.Verify(s,refunded with{Attributes=[24,11,15,17,10,2,0,24]},attr));Reject(()=>LiveBuild.Verify(s,refunded with{Character="Arna"},attr));
        var unlearned=s with{Credits=1,Ledger=s.Ledger-1,Gems=[2,1,1,2,1,1,0,1],Attributes=[25,11,15,17,10,0,1,24],Skills=[s.Skills[0],s.Skills[1] with{Learned=false}]};LiveBuild.Verify(s,unlearned,skill);Check(true);
        Reject(()=>LiveBuild.Verify(s,unlearned with{Skills=s.Skills},skill));Reject(()=>LiveBuild.Verify(s,unlearned with{Ledger=s.Ledger},skill));
        Check(LiveBuild.Parse(JsonSerializer.Serialize(s)).Token==s.Token);Reject(()=>LiveBuild.Parse("{\"error\":\"not loaded\"}"));Reject(()=>LiveBuild.Parse(JsonSerializer.Serialize(s with{Token="bad"})));Reject(()=>LiveBuild.Parse(JsonSerializer.Serialize(s with{Credits=7})));Reject(()=>LiveBuild.Parse(JsonSerializer.Serialize(s with{Gems=[]})));
        Check(LiveBuild.Missing(s,LiveBuild.Recipes[1])=="");Check(LiveBuild.Missing(s with{Gems=new int[8]},LiveBuild.Recipes[4]).Contains("蓝宝石"));
        Console.WriteLine($"PASS gem refund managed policy ({checks} checks)");
    }
}
