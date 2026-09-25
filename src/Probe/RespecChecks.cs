using System.Text.Json.Nodes;
using StoneshardCompanion;
internal static class RespecChecks
{
    private const string Salt="stOne!characters_v1!character_3!exitsave_1!shArd";
    private static int passed;
    private static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL respec: "+name);passed++;Console.WriteLine("PASS respec: "+name);}
    private static void Fails(Action action,string name){try{action();}catch(IOException){Check(true,name);return;}throw new Exception("FAIL respec expected refusal: "+name);}
    internal static JsonObject Fixture()=>JsonNode.Parse("""
    {"gameDataMap":{"wipeVersion":"0.9","compiler":"YYC","prologue":0,"permadeath":0,"seed":123},
     "characterDataMap":{"nameKey":"Jorgrim","LVL":8,"STR":16,"AGL":11,"PRC":11,"Vitality":11,"WIL":10,"AP":1,"SP":2,"XP":999,"Books_Read":["Warfare I"],"perksList":["reaver"],"Theory_&_Praxis_Skills":12,
      "buffs":["o_temp_incr_atr",7,1,"player","Crit_Avoid",3,"o_pass_skill_battle_hardiness",0,"o_temp_incr_atr",10,1,"player","Toxicity_Change",-0.01,"o_inv_wineskin",0]},
     "skillsDataMap":{"skillsAllDataList":["o_pass_skill_battle_hardiness",1,0,0,0,"o_skill_arc_cleave_ico",true,3,false,1,"o_skill_craft_ico",true,0,false,0,"o_skill_trap_search_ico",true,0,false,0,"o_skill_butchering_ico",true,0,false,0,"o_pass_skill_Sudden_Attacks",true,0,false,0,"o_skill_torch_strike_ico",true,0,false,0,"o_skill_unused_ico",0,0,1,0],"skillsPanelDataList":[["o_skill_trap_search","o_skill_arc_cleave","o_skill_craft",-4]]},
     "inventoryDataList":[["o_inv_moneybag",{"Stack":1500,"i_index":1},0,0,0,1,-4,0,0,"N/A"],["o_inv_moneybag",{"Stack":1000},0,2,0,1,-4,0,0,"N/A"],["Joust Armor",{"DEF":21,"quality":6,"Duration":360},-4,-4,0,-4,-4,true,0,"o_inv_armor"],["o_inv_bread",{"charge":2},2,0,0,2,-4,0,0,"N/A"]],
     "caravanStashDataList1":[["o_inv_moneybag",{"Stack":2000}]],"questsDataMap":{"complete":1},"timeDataMap":{"days":55}}
    """)!.AsObject();
    public static async Task Run(){
        if(PaidRespec.GameRunning())throw new IOException("Respec offline mutation checks require game closed (only temporary fixtures are changed).");
        string parent=Path.Combine(Path.GetTempPath(),"Stoneshard-respec-"+Guid.NewGuid().ToString("N")),root=Path.Combine(parent,"StoneShard"),backups=Path.Combine(parent,"Backups"),folder=Path.Combine(root,"characters_v1","character_3","exitsave_1");
        Directory.CreateDirectory(folder);string path=Path.Combine(folder,"data.sav"),index=Path.Combine(root,"characters_v1","characters.map");
        File.WriteAllBytes(index,PaidRespec.Encode(new JsonObject{["lastCharacter"]="character_3",["lastSave"]="exitsave_1"},"stOne!characters_v1!shArd"));
        void Write(JsonObject? node=null)=>File.WriteAllBytes(path,PaidRespec.Encode(node??Fixture(),Salt));
        Write();var service=new SaveManagerService(saveRoot:root,backupRoot:backups);var initial=File.ReadAllBytes(path);var preview=PaidRespec.Preview(root);
        Check(preview.Level==8&&preview.RefundedAttributes==6&&preview.AvailableAttributes==7&&preview.RefundedSkills==2&&preview.AvailableSkills==4,"refund actual allocations plus existing unused points; preserve initial actions");
        Check(preview.Crowns==2500&&File.ReadAllBytes(path).SequenceEqual(initial),"preview counts carried crowns only and never writes");
        var result=await service.RespecAsync(preview);var after=PaidRespec.Decode(File.ReadAllBytes(path),Salt);var c=after["characterDataMap"]!;
        Check(result.CrownsRemaining==500&&result.AttributePoints==7&&result.SkillPoints==4,"single transaction deducts exactly 2000 across purses");
        Check(c["STR"]!.GetValue<int>()==11&&c["AGL"]!.GetValue<int>()==10&&c["LVL"]!.GetValue<int>()==8&&c["XP"]!.GetValue<int>()==999,"attributes return to character baseline; level and XP unchanged");
        Check(after["skillsDataMap"]!["skillsAllDataList"]![1]!.GetValue<bool>()==false&&after["skillsDataMap"]!["skillsAllDataList"]![6]!.GetValue<bool>()==false&&after["skillsDataMap"]!["skillsAllDataList"]![7]!.GetValue<int>()==0,"paid skills and their cooldowns reset");
        Check(after["skillsDataMap"]!["skillsPanelDataList"]![0]![1]!.GetValue<int>()==-4&&after["skillsDataMap"]!["skillsPanelDataList"]![0]![0]!.GetValue<string>()=="o_skill_trap_search","removed skills leave hotbar; free actions remain");
        Check(c["buffs"]!.AsArray().Count==8&&c["buffs"]![6]!.GetValue<string>()=="o_inv_wineskin","skill-sourced temporary bonuses cleared, item effect retained");
        foreach(string key in new[]{"questsDataMap","timeDataMap","caravanStashDataList1"})Check(JsonNode.DeepEquals(after[key],Fixture()[key]),"unrelated data preserved: "+key);
        Check(JsonNode.DeepEquals(after["inventoryDataList"]![2],Fixture()["inventoryDataList"]![2]),"recovered unique armor survives respec untouched");
        Check(File.Exists(result.Backup)&&service.List().Single().Safety,"verified pre-respec backup is available as safety archive");
        var installed=File.ReadAllBytes(path);Fails(()=>new SaveEngine(root,backups).Respec(preview),"duplicate accepted preview cannot charge twice");Check(File.ReadAllBytes(path).SequenceEqual(installed),"duplicate refusal leaves save intact");
        Write();preview=PaidRespec.Preview(root);var changed=Fixture();changed["characterDataMap"]!["XP"]=1000;Write(changed);var changedBytes=File.ReadAllBytes(path);Fails(()=>new SaveEngine(root,backups).Respec(preview),"stale preview rejected");Check(changedBytes.SequenceEqual(File.ReadAllBytes(path)),"stale refusal does not charge");
        var low=Fixture();low["inventoryDataList"]![0]![1]!["Stack"]=0;Write(low);Fails(()=>PaidRespec.Preview(root),"banked crowns cannot pay insufficient carried funds");
        var unknown=Fixture();unknown["characterDataMap"]!["nameKey"]="Unknown";Write(unknown);Fails(()=>PaidRespec.Preview(root),"unknown starting profile blocked");
        Write();byte[] bad=File.ReadAllBytes(path);var decoded=PaidRespec.Decode(bad,Salt);Fails(()=>PaidRespec.Decode(bad,"wrong salt"),"salted checksum validated");
        preview=PaidRespec.Preview(root);Fails(()=>PaidRespec.Apply(root,preview,()=>throw new IOException("backup unavailable")),"backup failure aborts before debit");Check(bad.SequenceEqual(File.ReadAllBytes(path)),"backup failure leaves original bytes intact");
        var exact=Fixture();exact["inventoryDataList"]![0]![0]="o_inv_gold";exact["inventoryDataList"]![0]![1]!["Stack"]=1000;Write(exact);preview=PaidRespec.Preview(root);await service.RespecAsync(preview);after=PaidRespec.Decode(File.ReadAllBytes(path),Salt);Check(after["inventoryDataList"]!.AsArray().Count==3&&after["inventoryDataList"]![0]![1]!["Stack"]!.GetValue<int>()==0,"exact funds remove empty loose coin pile but retain empty purse");
        Console.WriteLine($"{passed} paid respec checks passed; temporary fixtures only: {parent}");
    }
}
