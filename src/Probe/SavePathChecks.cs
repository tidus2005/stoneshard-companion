using StoneshardCompanion;
using System.Security.Cryptography;

internal static class SavePathChecks
{
    public static async Task Run(string? alias,string? expectedRoot,string? workerPath=null)
    {
        string temp=Path.Combine(Path.GetTempPath(),"Stoneshard-path-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string first=Path.Combine(temp,"first"),second=Path.Combine(temp,"second"),actual=Path.Combine(temp,"actual");
        string? shared=null;
        try{
            Directory.CreateDirectory(actual);
            SaveCompatibilityChecks.CreateJunction(first,second);
            SaveCompatibilityChecks.CreateJunction(second,actual);
            string suffix=Path.Combine("新目录 [Mac]","backups");
            if(SavePaths.ResolveDirectoryRoot(Path.Combine(first,suffix))!=Path.Combine(actual,suffix))throw new Exception("Chained root resolution failed");
            Console.WriteLine("PASS chained roots preserve nonexistent suffix");
            Directory.Delete(second);SaveCompatibilityChecks.CreateJunction(second,first);
            try{SavePaths.ResolveDirectoryRoot(first);throw new Exception("Cycle accepted");}
            catch(IOException e) when(e.Message.Contains("循环")){Console.WriteLine("PASS cyclic roots refused");}
            Directory.Delete(first);Directory.Delete(second);
            if(alias is null)return;
            if(expectedRoot is null||!Path.IsPathFullyQualified(expectedRoot))throw new Exception("Supply an absolute expected root");
            string expected=Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedRoot));
            if(!SavePaths.ResolveDirectoryRoot(alias).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new Exception("Redirected root does not match expected network path");
            Console.WriteLine("PASS actual redirected root preserves UNC path");
            shared=Path.Combine(expected,"Stoneshard-path-"+Guid.NewGuid().ToString("N"));
            string backupAlias=Path.Combine(alias,Path.GetFileName(shared));
            string saves=Path.Combine(temp,"StoneShard"),data=Path.Combine(saves,"characters_v1","character_1","data.sav");
            Directory.CreateDirectory(Path.GetDirectoryName(data)!);File.WriteAllText(data,"ALIVE");
            var service=new SaveManagerService(workerPath,saveRoot:saves,backupRoot:backupAlias);
            var result=await service.RunAsync(SaveOperation.Latest);
            if(Path.GetDirectoryName(result.Archive)!=shared||!File.Exists(result.Archive))throw new Exception("Archive was published outside the expected share");
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(result.Archive)))!=result.Hash)throw new Exception("Shared checksum failed");
            Console.WriteLine("PASS worker backup and latest copy on actual share");
            var listed=await service.ListAsync();
            if(listed.Count!=2)throw new Exception("Shared history listing failed");
            File.WriteAllText(data,"CHANGED");
            var restored=await service.RunAsync(SaveOperation.Restore,result.Archive);
            if(File.ReadAllText(data)!="ALIVE"||restored.SafetyArchive is null)throw new Exception("Shared restore failed");
            Console.WriteLine("PASS shared history and restore with safety backup");
        }finally{
            // Unlink only these fixtures before any recursive cleanup.
            foreach(string link in new[]{first,second})if(new DirectoryInfo(link).LinkTarget is not null)Directory.Delete(link);
            foreach(string? path in new[]{shared,temp}){
                if(path is null)continue;
                string parent=path==temp?Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())):Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedRoot!));
                if(Path.GetDirectoryName(Path.GetFullPath(path))!=parent||!Path.GetFileName(path).StartsWith("Stoneshard-path-"))throw new Exception("Unsafe fixture cleanup");
                if(Directory.Exists(path))Directory.Delete(path,true);
            }
        }
    }
}
