using StoneshardCompanion;
using System.Text;

internal static class GameCompatibilityChecks
{
    public static void Run(){
        int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        byte[] Form(params (string Name,byte[] Data)[] chunks){
            using var body=new MemoryStream();using var writer=new BinaryWriter(body,Encoding.ASCII,true);
            foreach(var (name,data) in chunks){writer.Write(Encoding.ASCII.GetBytes(name));writer.Write(data.Length);writer.Write(data);}
            using var all=new MemoryStream();using var output=new BinaryWriter(all);output.Write("FORM"u8);output.Write((uint)body.Length);output.Write(body.ToArray());return all.ToArray();
        }
        GameDataRuntime Inspect(byte[] bytes){using var stream=new MemoryStream(bytes);stream.Position=Math.Min(3,bytes.Length);long before=stream.Position;var result=GameCompatibility.InspectRuntime(stream);Check(stream.Position==before,"probe preserves stream position");return result;}
        Check(Inspect(Form(("GEN8",new byte[8])))==GameDataRuntime.Native,"native container without bytecode");
        Check(Inspect(Form(("CODE",[])))==GameDataRuntime.Native,"empty bytecode chunk is not VM");
        Check(Inspect(Form(("GEN8",new byte[2]),("CODE",new byte[8])))==GameDataRuntime.VirtualMachine,"nonempty bytecode detects modbranch runtime");
        Check(Inspect("not a game"u8.ToArray())==GameDataRuntime.Unknown,"unknown container");
        var truncated=Form(("CODE",new byte[8]));Array.Resize(ref truncated,truncated.Length-1);
        Check(Inspect(truncated)==GameDataRuntime.Unknown,"truncated container never implies compatibility");
        var overflow=Form(("CODE",[]));Array.Fill(overflow,(byte)255,12,4);
        Check(Inspect(overflow)==GameDataRuntime.Unknown,"oversized chunk rejected");
        Check(GameCompatibility.Rejection("0.9.4.25",GameSession.SupportedHash,GameCompatibility.SupportedDataHash,GameDataRuntime.Native)==null,"known pair accepted");
        Check(GameCompatibility.Rejection("0.9.4.25",GameSession.UncappedHash,GameCompatibility.SupportedDataHash,GameDataRuntime.Native)==null,"verified level-cap variant retained");
        Check(GameCompatibility.Rejection("0.9.4.25",GameSession.SupportedHash,GameCompatibility.IsolatedDataHash,GameDataRuntime.Native)==null,"existing isolated save namespace retained");
        Check(GameCompatibility.Rejection("0.9.4.25",GameSession.SupportedHash,"unknown",GameDataRuntime.Native)!.Contains("数据包"),"modified native data still rejected");
        Check(GameCompatibility.Rejection("0.9.4.25","unknown","unknown",GameDataRuntime.VirtualMachine)!.Contains("VM"),"VM diagnosis has priority over generic executable mismatch");
        Check(GameCompatibility.Rejection("other","unknown",GameCompatibility.SupportedDataHash,GameDataRuntime.Unknown)!.Contains("可执行文件"),"unrecognized executable never injected");
        Console.WriteLine($"{checks} game compatibility checks passed; no game or saves accessed.");
    }
}
