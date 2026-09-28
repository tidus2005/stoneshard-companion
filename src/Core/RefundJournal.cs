using System.Text.Json;

namespace StoneshardCompanion;

// One journal per game process/start time. A lost acknowledgement must not turn
// into a second charge, including after restarting only the assistant.
public sealed class RefundJournal(string path)
{
    public sealed record Entry(bool Pending,string Stage,string Backup,LiveBuildSnapshot Before,PointRefund Request,LiveBuildSnapshot? After=null,string? Error=null);
    public void RequireClear(){
        if(!File.Exists(path))return;
        Entry entry;
        try{entry=JsonSerializer.Deserialize<Entry>(File.ReadAllText(path))??throw new IOException("退点记录为空。");}
        catch(JsonException e){throw new IOException("上次退点记录无法读取，为防止重复扣除，暂不允许再次退点。",e);}
        if(entry.Pending)throw new IOException("上次退点已提交但尚未完成核验，本次未执行。请先退出游戏并核对存档，勿重复退点。\n保险备份："+entry.Backup);
    }
    public void Write(Entry entry){
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(entry,new JsonSerializerOptions{WriteIndented=true});
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}
            File.Move(temporary,path,true);
        }finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
