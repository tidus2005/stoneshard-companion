using System.Text;
using System.Text.Json;

namespace StoneshardCompanion;

public static class SaveWorker
{
    public sealed record Request(string Operation,string SaveRoot,string BackupRoot,string? Archive);
    // Run before WPF startup / single-instance checks. This worker never opens
    // a window, discovers games, injects the bridge, or launches another program.
    public static int Run(string[] args)
    {
        // A WinExe worker has redirected pipes but no console. Setting the
        // console code page fails there; encode the pipe explicitly instead.
        using var output=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true};
        string stage="读取任务";
        try{
            if(args.Length!=2||args[0]!="--save-worker")throw new IOException("存档任务参数不完整。");
            var request=JsonSerializer.Deserialize<Request>(File.ReadAllText(args[1]))??throw new IOException("存档任务为空。");
            if(!Enum.TryParse<SaveOperation>(request.Operation,out var operation)||!Enum.IsDefined(operation))throw new IOException("不支持的存档操作。");
            stage="解析存档与备份目录";
            var engine=new SaveEngine(request.SaveRoot,request.BackupRoot,message=>{stage=message;output.WriteLine(message);});
            stage=operation==SaveOperation.Restore?"准备还原":"准备备份";
            var result=engine.Run(operation,request.Archive);
            output.WriteLine("@@RESULT@@"+JsonSerializer.Serialize(new{Ok=true,Result=result}));return 0;
        }catch(Exception e){
            string message=e is UnauthorizedAccessException?"存档或备份目录没有写入权限。请检查 Windows 权限和共享文件夹是否可写。"+e.Message:e.Message;
            string version=System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)??"未知";
            output.WriteLine("@@RESULT@@"+JsonSerializer.Serialize(new{Ok=false,Error=$"[{version}] {stage}：{message}（{e.GetType().Name} / 0x{e.HResult:X8}）"}));return 1;
        }
    }
}
