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
        try{
            if(args.Length!=2||args[0]!="--save-worker")throw new IOException("存档任务参数不完整。");
            var request=JsonSerializer.Deserialize<Request>(File.ReadAllText(args[1]))??throw new IOException("存档任务为空。");
            if(!Enum.TryParse<SaveOperation>(request.Operation,out var operation)||!Enum.IsDefined(operation))throw new IOException("不支持的存档操作。");
            var result=new SaveEngine(request.SaveRoot,request.BackupRoot,output.WriteLine).Run(operation,request.Archive);
            output.WriteLine("@@RESULT@@"+JsonSerializer.Serialize(new{Ok=true,Result=result}));return 0;
        }catch(Exception e){
            string message=e is UnauthorizedAccessException?"存档或备份目录没有写入权限。请检查 Windows 权限和共享文件夹是否可写。"+e.Message:e.Message;
            output.WriteLine("@@RESULT@@"+JsonSerializer.Serialize(new{Ok=false,Error=message}));return 1;
        }
    }
}
