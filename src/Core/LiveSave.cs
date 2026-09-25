using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace StoneshardCompanion;

public sealed record LiveSaveBaseline(string Character,IReadOnlyDictionary<string,string> Hashes);
public sealed record LiveSaveReceipt(string Character,string Slot,string Fingerprint);
public sealed record LiveSaveResult(LiveSaveReceipt Receipt,string SafetyArchive,string? Archive,string? BackupError);

// Observe native saves; never manufacture or rename slots while the game runs.
public static class LiveSave
{
    private const string IndexSalt="stOne!characters_v1!shArd";
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static string FilePath(string root,params string[] parts){
        string path=SavePaths.ResolveDirectoryRoot(root);
        foreach(string part in parts){path=Path.Combine(path,part);SavePaths.AssertNotLink(path);}
        return path;
    }
    private static JsonObject Index(string root)=>PaidRespec.Decode(File.ReadAllBytes(FilePath(root,"characters_v1","characters.map")),IndexSalt);
    private static string Character(JsonObject index){
        string value=index["lastCharacter"]?.GetValue<string>()??"";
        if(!Regex.IsMatch(value,"^character_[0-9]+$"))throw new IOException("无法确认当前角色，未发起存档。");
        return value;
    }
    public static LiveSaveBaseline Capture(string root){
        string character=Character(Index(root));var hashes=new Dictionary<string,string>(StringComparer.Ordinal);
        string folder=FilePath(root,"characters_v1",character);
        foreach(string dir in Directory.EnumerateDirectories(folder,"autosave_*")){
            string slot=Path.GetFileName(dir);if(!Regex.IsMatch(slot,"^autosave_[0-9]+$"))continue;
            string path=FilePath(root,"characters_v1",character,slot,"data.sav");
            if(File.Exists(path)){
                string map=FilePath(root,"characters_v1",character,slot,"save.map"),png=FilePath(root,"characters_v1",character,slot,"preview.png");
                hashes[slot]=Hash(File.ReadAllBytes(path))+(File.Exists(map)?Hash(File.ReadAllBytes(map)):"")+(File.Exists(png)?Hash(File.ReadAllBytes(png)):"");
            }
        }
        return new(character,hashes);
    }
    public static LiveSaveReceipt? Inspect(string root,LiveSaveBaseline before){
        var index=Index(root);if(Character(index)!=before.Character)throw new InvalidOperationException("当前角色发生变化，无法确认存档；不会重复保存。");
        string slot=index["lastSave"]?.GetValue<string>()??"";
        if(!Regex.IsMatch(slot,"^autosave_[0-9]+$"))return null;
        string file=FilePath(root,"characters_v1",before.Character,slot,"data.sav");
        byte[] data=File.ReadAllBytes(file);string hash=Hash(data);
        var body=PaidRespec.Decode(data,$"stOne!characters_v1!{before.Character}!{slot}!shArd");
        if(body["characterDataMap"] is not JsonObject||body["gameDataMap"] is not JsonObject||body["inventoryDataList"] is not JsonArray)throw new IOException("新存档内容不完整。");
        // Native load requires its metadata, and the native flow writes a preview.
        byte[] map=File.ReadAllBytes(FilePath(root,"characters_v1",before.Character,slot,"save.map"));
        var metadata=PaidRespec.Decode(map,$"stOne!characters_v1!{before.Character}!{slot}!shArd");
        string? valid=metadata["valid"]?.ToJsonString();
        if(valid is not ("true" or "1" or "1.0"))throw new IOException("游戏尚未将存档标记为有效。");
        byte[] png=File.ReadAllBytes(FilePath(root,"characters_v1",before.Character,slot,"preview.png"));
        if(png.Length<8||!png.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new IOException("存档截图尚未写入。");
        string fingerprint=hash+Hash(map)+Hash(png);
        if(before.Hashes.TryGetValue(slot,out string? old)&&old==fingerprint)return null;
        return new(before.Character,slot,fingerprint);
    }
    public static async Task<LiveSaveReceipt> WaitAsync(string root,LiveSaveBaseline before,TimeSpan? timeout=null){
        var watch=System.Diagnostics.Stopwatch.StartNew();LiveSaveReceipt? prior=null;
        while(watch.Elapsed<(timeout??TimeSpan.FromSeconds(30))){
            await Task.Delay(350);
            try{
                var current=await Task.Run(()=>Inspect(root,before));
                if(current is not null&&current==prior)return current;
                prior=current;
            }catch(Exception e) when(e is IOException or InvalidDataException or System.Text.Json.JsonException){prior=null;}
        }
        throw new TimeoutException("尚未确认新的完整存档。请查看游戏提示和原版 Load 列表；不会自动重复保存。");
    }
}
