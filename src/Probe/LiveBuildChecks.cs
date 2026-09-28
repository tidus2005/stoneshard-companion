using StoneshardCompanion;
using System.Text.Json;
using System.Text.Json.Nodes;
internal static class LiveBuildChecks
{
    public static void Run(){
        int checks=0;void Check(bool ok){checks++;if(!ok)throw new Exception("Gem refund check "+checks);}
        void Reject(Action a){bool rejected=false;try{a();}catch(IOException){rejected=true;}Check(rejected);}
        int[] Gems(params (int index,int count)[] entries){var result=new int[LiveBuild.GemKeys.Length];foreach(var (i,n) in entries)result[i]=n;return result;}
        var s=new LiveBuildSnapshot("Jorgrim","0123456789abcdef",376,[25,11,15,17,10,0,0,24],[11,10,11,11,10],[new(1,"o_skill_a",0,true,"o_skill_b"),new(2,"o_skill_b",0,true,"")],2,37,1000000372,[5,2,4,3,2,2,2,2,15,10,8,6,3,2],true,"CaravanCamp",["","","","",""]);
        var attr=new PointRefund(true,0,Gems((7,1)));var skill=new PointRefund(false,2,Gems((5,1),(2,1)));
        LiveBuild.Validate(s,attr);LiveBuild.Validate(s,skill);Check(true);
        Reject(()=>LiveBuild.Validate(s,skill with{Id=1}));Reject(()=>LiveBuild.Validate(s,attr with{Id=4}));Reject(()=>LiveBuild.Validate(s,attr with{Id=5}));Reject(()=>LiveBuild.Validate(s,skill with{Id=999}));
        Reject(()=>LiveBuild.Validate(s with{Credits=0},attr));Reject(()=>LiveBuild.Validate(s with{SafePlace=false},skill));
        Reject(()=>LiveBuild.Validate(s with{Gems=Gems()},attr));Reject(()=>LiveBuild.Validate(s with{AttributeReasons=["dependency","","","",""]},attr));
        LiveBuild.Validate(s with{Crowns=0},attr);Check(true);
        foreach(var materials in new[]{Gems((7,1)),Gems((5,1),(2,1)),Gems((0,1),(1,1),(2,2)),Gems((8,15)),Gems((9,10)),Gems((0,5)),Gems((7,2))}){
            LiveBuild.Validate(s,attr with{Materials=materials});LiveBuild.Validate(s,skill with{Materials=materials});Check(true);
        }
        Reject(()=>LiveBuild.Validate(s,attr with{Materials=Gems((4,1),(8,4),(9,1))})); // 595
        foreach(var invalid in new[]{Gems(),new int[8],Gems((0,-1),(7,1)),Gems((0,128),(7,1)),Gems((0,int.MaxValue)),Gems((7,3))})Reject(()=>LiveBuild.Validate(s,attr with{Materials=invalid}));
        Reject(()=>LiveBuild.Validate(s,attr with{Materials=null!}));
        Check(attr.Payload(s)=="0123456789abcdef|A|0|0,0,0,0,0,0,0,1,0,0,0,0,0,0");
        var refunded=s with{Credits=1,Ledger=s.Ledger-1,Gems=s.Gems.Select((n,i)=>n-attr.Materials[i]).ToArray(),Attributes=[24,11,15,17,10,1,0,24]};LiveBuild.Verify(s,refunded,attr);Check(true);
        Reject(()=>LiveBuild.Verify(s,refunded with{Crowns=375},attr));Reject(()=>LiveBuild.Verify(s,refunded with{Gems=s.Gems},attr));Reject(()=>LiveBuild.Verify(s,refunded with{Credits=2},attr));Reject(()=>LiveBuild.Verify(s,refunded with{Attributes=[24,11,15,17,10,2,0,24]},attr));Reject(()=>LiveBuild.Verify(s,refunded with{Character="Arna"},attr));
        var wrongGem=(int[])refunded.Gems.Clone();wrongGem[7]++;wrongGem[5]--;Reject(()=>LiveBuild.Verify(s,refunded with{Gems=wrongGem},attr));
        var unlearned=s with{Credits=1,Ledger=s.Ledger-1,Gems=s.Gems.Select((n,i)=>n-skill.Materials[i]).ToArray(),Attributes=[25,11,15,17,10,0,1,24],Skills=[s.Skills[0],s.Skills[1] with{Learned=false}]};LiveBuild.Verify(s,unlearned,skill);Check(true);
        Reject(()=>LiveBuild.Verify(s,unlearned with{Skills=s.Skills},skill));Reject(()=>LiveBuild.Verify(s,unlearned with{Ledger=s.Ledger},skill));
        Reject(()=>LiveBuild.Verify(s,unlearned with{Attributes=s.Attributes},skill)); // Gems consumed, SP not returned.
        Check(LiveBuild.PointSummary(s,unlearned)=="属性点 0 → 0；技能点 0 → 1");
        for(uint flags=0;flags<64;flags++)Check(LiveBuild.RefundUiClear(flags)==(flags is 0 or 8));
        // Consecutive refunds conserve learned+unspent points, including passive skills.
        var consecutive=s with{Skills=[new(11,"o_pass_skill_sprint_training",15,true,""),new(12,"o_pass_skill_no_time_to_linger",15,true,""),new(13,"o_skill_sudden_strike_ico",15,true,"")],Credits=3,Ledger=1000000373,Gems=Gems((7,3))};
        foreach(int id in new[]{11,12,13}){
            var request=new PointRefund(false,id,Gems((7,1)));var values=(int[])consecutive.Attributes.Clone();values[6]++;
            var next=consecutive with{Attributes=values,Skills=consecutive.Skills.Select(x=>x.Id==id?x with{Learned=false}:x).ToArray(),Credits=consecutive.Credits-1,Ledger=consecutive.Ledger-1,Gems=Gems((7,consecutive.Gems[7]-1))};
            LiveBuild.Verify(consecutive,next,request);Check(next.Attributes[6]+next.Skills.Count(x=>x.Learned)==3);Reject(()=>LiveBuild.Validate(next,request));consecutive=next;
        }
        Check(consecutive.Attributes[6]==3&&consecutive.Gems.Sum()==0);
        string journalPath=Path.Combine(Path.GetTempPath(),"Stoneshard-refund-journal-"+Guid.NewGuid().ToString("N"),"session.json");
        var journal=new RefundJournal(journalPath);journal.RequireClear();Check(true);
        journal.Write(new(true,"submitted","insurance.zip",s,skill));Reject(()=>new RefundJournal(journalPath).RequireClear());
        journal.Write(new(true,"save failed","insurance.zip",s,skill,unlearned,"timeout"));Reject(()=>new RefundJournal(journalPath).RequireClear());
        journal.Write(new(false,"verified","insurance.zip",s,skill,unlearned));new RefundJournal(journalPath).RequireClear();Check(true);
        File.WriteAllText(journalPath,"broken");Reject(()=>new RefundJournal(journalPath).RequireClear());
        Check(LiveBuild.Parse(JsonSerializer.Serialize(s)).Token==s.Token);Reject(()=>LiveBuild.Parse("{\"error\":\"not loaded\"}"));Reject(()=>LiveBuild.Parse(JsonSerializer.Serialize(s with{Token="bad"})));Reject(()=>LiveBuild.Parse(JsonSerializer.Serialize(s with{Credits=7})));Reject(()=>LiveBuild.Parse(JsonSerializer.Serialize(s with{Gems=new int[8]})));Reject(()=>LiveBuild.Parse(JsonSerializer.Serialize(s with{Gems=Gems((0,128))})));
        Check(LiveBuild.Missing(s,attr.Materials)=="");Check(LiveBuild.Missing(s with{Gems=Gems()},attr.Materials).Contains("钻石"));
        Check(LiveBuild.SuggestMaterials(Gems((0,4))) is null);Check(LiveBuild.MaterialValue(LiveBuild.SuggestMaterials(Gems((0,5)))!)==625);
        Check(LiveBuild.SuggestMaterials(Gems((7,1),(2,4)))!.SequenceEqual(Gems((7,1))));
        Check(LiveBuild.MaterialValue(LiveBuild.SuggestMaterials(Gems((6,1),(0,1),(5,1),(2,1)))!)==600);
        // Independent exhaustive oracle for the bounded planner.
        var random=new Random(600);
        for(int trial=0;trial<80;trial++){
            int[] inventory=Enumerable.Range(0,LiveBuild.GemKeys.Length).Select(_=>random.Next(3)).ToArray();int best=int.MaxValue,fewest=int.MaxValue;
            void Search(int g,int value,int count){if(g==LiveBuild.GemKeys.Length){if(value>=600&&(value<best||value==best&&count<fewest)){best=value;fewest=count;}return;}for(int n=0;n<=inventory[g];n++)Search(g+1,value+n*LiveBuild.GemValues[g],count+n);}
            Search(0,0,0);var plan=LiveBuild.SuggestMaterials(inventory);
            Check(best==int.MaxValue?plan is null:plan is not null&&LiveBuild.MaterialValue(plan)==best&&plan.Sum()==fewest&&plan.Select((n,i)=>n<=inventory[i]).All(x=>x));
        }
        Check(LiveBuild.MaterialValue(LiveBuild.SuggestMaterials(Enumerable.Repeat(127,LiveBuild.GemKeys.Length).ToArray())!)==600);
        // Saved-material verification includes amber/agate and excludes storage.
        string folder=Path.Combine(Path.GetTempPath(),"Stoneshard-gem-value-"+Guid.NewGuid().ToString("N"),"character_3","save_1");Directory.CreateDirectory(folder);string path=Path.Combine(folder,"data.sav");
        var saved=s with{Gems=Gems((8,2),(9,3))};var data=new JsonObject{["characterDataMap"]=new JsonObject{[LiveBuild.LedgerKey]=(double)s.Ledger},["inventoryDataList"]=new JsonArray(),["caravanStashDataList1"]=new JsonArray(new JsonArray("o_inv_diamond"))};
        for(int g=0;g<LiveBuild.GemKeys.Length;g++)for(int n=0;n<saved.Gems[g];n++)data["inventoryDataList"]!.AsArray().Add(new JsonArray(LiveBuild.GemKeys[g]));
        File.WriteAllBytes(path,PaidRespec.Encode(data,"stOne!characters_v1!character_3!save_1!shArd"));LiveBuild.VerifySavedMaterials(path,saved);Check(true);
        Reject(()=>LiveBuild.VerifySavedMaterials(path,saved with{Gems=Gems((8,2),(9,2))}));Reject(()=>LiveBuild.VerifySavedMaterials(path,saved with{Ledger=s.Ledger-1}));
        Console.WriteLine($"PASS gem value refund managed policy ({checks} checks); temporary fixture only: {folder}");
    }
}
