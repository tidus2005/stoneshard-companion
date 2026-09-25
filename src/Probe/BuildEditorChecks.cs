using StoneshardCompanion;
using System.Text.Json.Nodes;

internal static class BuildEditorChecks
{
    public static async Task Run(){
        if(PaidRespec.GameRunning())throw new IOException("Close the game before offline checks.");
        int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);passed++;}
        void Reject(Action action,string name){try{action();}catch(IOException){Check(true,name);return;}throw new Exception("Expected rejection: "+name);}
        var nested=new JsonObject{["name"]="约戈里姆",["map"]="{ \"tiles\": [ 1.0 ] }"};
        byte[] encoded=PaidRespec.Encode(nested,"test");using(var compressed=new MemoryStream(encoded))using(var inflate=new System.IO.Compression.ZLibStream(compressed,System.IO.Compression.CompressionMode.Decompress))using(var plain=new StreamReader(inflate)){var json=plain.ReadToEnd();Check(!json.Contains(@"\u0022")&&json.Contains("约戈里姆"),"game JSON encoder preserves native nested quotes and UTF8 without HTML escaping");}
        Check(JsonNode.DeepEquals(nested,PaidRespec.Decode(encoded,"test")),"nested map and localized strings roundtrip");
        string parent=Path.Combine(Path.GetTempPath(),"Stoneshard-build-"+Guid.NewGuid().ToString("N")),root=Path.Combine(parent,"StoneShard"),backups=Path.Combine(parent,"Backups"),folder=Path.Combine(root,"characters_v1","character_3","save_1");
        Directory.CreateDirectory(folder);string path=Path.Combine(folder,"data.sav"),index=Path.Combine(root,"characters_v1","characters.map"),salt="stOne!characters_v1!character_3!save_1!shArd";
        File.WriteAllBytes(index,PaidRespec.Encode(new JsonObject{["lastCharacter"]="character_3",["lastSave"]="save_1"},"stOne!characters_v1!shArd"));
        string metadata=Path.Combine(folder,"save.map");File.WriteAllBytes(metadata,PaidRespec.Encode(new JsonObject{["valid"]=true},salt));
        void Write(JsonObject? data=null)=>File.WriteAllBytes(path,PaidRespec.Encode(data??RespecChecks.Fixture(),salt));
        Write();var original=File.ReadAllBytes(path);var snapshot=BuildEditor.Read(root);var draft=BuildEditor.Draft(snapshot);
        Check(!BuildEditor.Validate(snapshot,draft).Changed&&original.SequenceEqual(File.ReadAllBytes(path)),"read and unchanged draft never write");
        Reject(()=>new SaveEngine(root,backups).EditBuild(snapshot,draft),"no-op does not charge");
        draft.Attributes[0]--;draft.Attributes[1]++;draft=draft with{Skills=draft.Skills.Where(x=>x!="o_pass_skill_battle_hardiness"&&x!="o_skill_arc_cleave_ico").Append("o_skill_unused_ico").ToArray()};
        var balance=BuildEditor.Validate(snapshot,draft);Check(balance.AP==1&&balance.SP==3&&balance.Changed,"partial refund and reallocation conserve both pools");
        var service=new SaveManagerService(saveRoot:root,backupRoot:backups);var result=await service.EditBuildAsync(snapshot,draft);var after=PaidRespec.Decode(File.ReadAllBytes(path),salt);
        Check(result.Crowns==2000&&result.AP==1&&result.SP==3,"single 500 crown debit");
        Check(BuildEditor.Read(root).Attributes.SequenceEqual(new[]{15,12,11,11,10}),"chosen attributes persist");
        Check(BuildEditor.Read(root).Skills.Single(x=>x.Key=="o_skill_unused_ico").Learned,"newly selected skill persists");
        Check(BuildEditor.Read(root).Skills.Where(x=>x.Fixed).All(x=>x.Learned),"free actions retained without refunds");
        Check(BuildEditor.Int(after["characterDataMap"]!["LVL"])==8&&BuildEditor.Int(after["characterDataMap"]!["XP"])==999,"level and XP unchanged");
        Check(after["skillsDataMap"]!["skillsAllDataList"]![7]!.GetValue<int>()==0,"removed skill cooldown cleared");
        Check(after["skillsDataMap"]!["skillsAllDataList"]![38]!.GetValue<int>()==1,"skill book metadata retained");
        Check(after["skillsDataMap"]!["skillsPanelDataList"]![0]![1]!.GetValue<int>()==-4,"removed active skill leaves hotbar");
        Check(after["characterDataMap"]!["buffs"]!.AsArray().Count==8,"removed passive source cleared while item effect remains");
        foreach(string key in new[]{"questsDataMap","timeDataMap","caravanStashDataList1"})Check(JsonNode.DeepEquals(after[key],RespecChecks.Fixture()[key]),"preserved "+key);
        Check(JsonNode.DeepEquals(after["inventoryDataList"]![2],RespecChecks.Fixture()["inventoryDataList"]![2]),"unique armor untouched");
        Check(File.Exists(result.Backup)&&service.List().Single().Safety,"verified safety archive created");
        byte[] committed=File.ReadAllBytes(path);Reject(()=>new SaveEngine(root,backups).EditBuild(snapshot,draft),"repeat submission cannot charge twice");Check(committed.SequenceEqual(File.ReadAllBytes(path)),"duplicate rejection preserves bytes");
        Write();snapshot=BuildEditor.Read(root);var invalid=BuildEditor.Draft(snapshot);invalid.Attributes[0]=10;Reject(()=>BuildEditor.Validate(snapshot,invalid),"below character baseline rejected");invalid.Attributes[0]=31;Reject(()=>BuildEditor.Validate(snapshot,invalid),"above 30 rejected");invalid.Attributes[0]=18;Reject(()=>BuildEditor.Validate(snapshot,invalid),"attribute overspend rejected");
        invalid=BuildEditor.Draft(snapshot);Reject(()=>BuildEditor.Validate(snapshot with{SP=0},invalid with{Skills=[..invalid.Skills,"o_skill_unused_ico"]}),"skill overspend rejected");Reject(()=>BuildEditor.Validate(snapshot,invalid with{Skills=invalid.Skills.Where(x=>x!="o_skill_craft_ico").ToArray()}),"free action refund rejected");
        Reject(()=>BuildEditor.Validate(snapshot,invalid with{Skills=[..invalid.Skills,"o_skill_fake_ico"]}),"unknown skill rejected");Reject(()=>BuildEditor.Validate(snapshot,invalid with{Skills=[..invalid.Skills,invalid.Skills[0]]}),"duplicate skill rejected");
        Reject(()=>BuildEditor.Apply(root,snapshot,draft,()=>throw new IOException("backup failed")),"backup failure prevents commit");Check(original.SequenceEqual(File.ReadAllBytes(path)),"backup failure preserves original");
        var changed=RespecChecks.Fixture();changed["characterDataMap"]!["XP"]=1000;Write(changed);Reject(()=>new SaveEngine(root,backups).EditBuild(snapshot,draft),"stale source rejected");
        var low=RespecChecks.Fixture();low["inventoryDataList"]![0]![1]!["Stack"]=100;low["inventoryDataList"]![1]![1]!["Stack"]=0;Write(low);snapshot=BuildEditor.Read(root);Reject(()=>new SaveEngine(root,backups).EditBuild(snapshot,draft),"caravan money cannot pay insufficient carried funds");
        var exact=RespecChecks.Fixture();exact["inventoryDataList"]![0]![0]="o_inv_gold";exact["inventoryDataList"]![0]![1]!["Stack"]=200;exact["inventoryDataList"]![1]![1]!["Stack"]=300;Write(exact);snapshot=BuildEditor.Read(root);await service.EditBuildAsync(snapshot,draft);after=PaidRespec.Decode(File.ReadAllBytes(path),salt);Check(BuildEditor.Read(root).Crowns==0&&after["inventoryDataList"]!.AsArray().Count==3,"exact mixed payment removes empty loose coins but keeps purse");
        File.WriteAllBytes(metadata,PaidRespec.Encode(new JsonObject{["valid"]=false},salt));Reject(()=>BuildEditor.Read(root),"invalid or consumed native slot rejected");
        Console.WriteLine($"{passed} build editor checks passed; fixtures: {parent}");
    }
}
