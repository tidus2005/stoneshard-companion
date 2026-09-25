using System.Text.Json.Nodes;
using StoneshardCompanion;

internal static class LiveSaveChecks
{
    public static async Task Run(){
        string parent=Path.Combine(Path.GetTempPath(),"Stoneshard-live-save-"+Guid.NewGuid().ToString("N")),root=Path.Combine(parent,"StoneShard"),chars=Path.Combine(root,"characters_v1"),slot=Path.Combine(chars,"character_3","autosave_1");
        Directory.CreateDirectory(slot);string index=Path.Combine(chars,"characters.map");
        const string salt="stOne!characters_v1!character_3!autosave_1!shArd";
        void Index(string name="character_3",string save="autosave_1")=>File.WriteAllBytes(index,PaidRespec.Encode(new JsonObject{["lastCharacter"]=name,["lastSave"]=save},"stOne!characters_v1!shArd"));
        void Data(int n)=>File.WriteAllBytes(Path.Combine(slot,"data.sav"),PaidRespec.Encode(new JsonObject{["characterDataMap"]=new JsonObject{["XP"]=n},["gameDataMap"]=new JsonObject(),["inventoryDataList"]=new JsonArray()},salt));
        void Check(bool ok,string name){if(!ok)throw new Exception("FAIL live-save: "+name);Console.WriteLine("PASS live-save: "+name);}
        Index();Data(1);File.WriteAllBytes(Path.Combine(slot,"save.map"),PaidRespec.Encode(new JsonObject{["valid"]=true},salt));
        File.WriteAllBytes(Path.Combine(slot,"preview.png"),[137,80,78,71,13,10,26,10,0]);
        var before=LiveSave.Capture(root);Check(LiveSave.Inspect(root,before)==null,"unchanged save is never a successful save");
        Data(2);Check(LiveSave.Inspect(root,before)?.Slot=="autosave_1","new data with checksum and native index accepted");
        Index(save:"exitsave_1");Check(LiveSave.Inspect(root,before)==null,"exit save does not count as new autosave");Index();
        Index("character_4");bool rejected=false;try{LiveSave.Inspect(root,before);}catch(InvalidOperationException){rejected=true;}Check(rejected,"character changes rejected");Index();
        File.WriteAllBytes(Path.Combine(slot,"data.sav"),[1,2,3]);rejected=false;try{LiveSave.Inspect(root,before);}catch(Exception e) when(e is IOException or InvalidDataException){rejected=true;}Check(rejected,"incomplete data rejected");Data(2);
        before=LiveSave.Capture(root);rejected=false;try{await LiveSave.WaitAsync(root,before,TimeSpan.FromMilliseconds(400));}catch(TimeoutException){rejected=true;}Check(rejected,"timeout never reports success");
        var service=new SaveManagerService(saveRoot:root,backupRoot:Path.Combine(parent,"Backups"));int calls=0;
        var result=await service.SaveNowAsync(()=>{Check(service.Busy,"exclusive save operation held");calls++;Data(3);return Task.CompletedTask;});
        Check(calls==1&&result.Receipt.Slot=="autosave_1"&&File.Exists(result.SafetyArchive)&&File.Exists(result.Archive),"single native request; complete verified backups before and after");
        Check(!service.Busy,"service released after save");
        rejected=false;try{await service.SaveNowAsync(()=>{calls++;throw new IOException("native rejected");});}catch(IOException){rejected=true;}
        Check(rejected&&calls==2&&!service.Busy,"failed native request is not retried; gate released");
        before=LiveSave.Capture(root);
        File.WriteAllBytes(Path.Combine(slot,"save.map"),PaidRespec.Encode(new JsonObject{["valid"]=false},salt));
        rejected=false;try{LiveSave.Inspect(root,before);}catch(IOException){rejected=true;}Check(rejected,"native invalid metadata rejected");
        File.WriteAllBytes(Path.Combine(slot,"save.map"),PaidRespec.Encode(new JsonObject{["valid"]=true,["dateTime"]=123},salt));
        Check(LiveSave.Inspect(root,before) is not null,"same progress saved again with new metadata is accepted");
        var entered=new TaskCompletionSource();var release=new TaskCompletionSource();
        var saving=service.SaveNowAsync(async ()=>{entered.SetResult();await release.Task;Data(4);});await entered.Task;
        rejected=false;try{await service.SaveNowAsync(()=>throw new Exception("duplicate must not dispatch"));}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"duplicate button press cannot dispatch another native save");release.SetResult();await saving;
        // These temporary artifacts are deliberately retained for investigation.
    }
}
