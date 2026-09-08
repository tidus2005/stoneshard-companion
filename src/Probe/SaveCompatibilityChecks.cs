using StoneshardCompanion;
using System.IO.Compression;
using System.Security.Cryptography;

internal static class SaveCompatibilityChecks
{
    private static int passed;
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    private static void Refused(Action run,string expected)
    {
        try{run();}catch(IOException e){Check(e.Message.Contains(expected),"managed archive refused: "+expected);return;}
        throw new Exception("Expected refusal: "+expected);
    }
    public static async Task Run(string? workerPath)
    {
        string temp=Path.Combine(Path.GetTempPath(),"Stoneshard-compat-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try{
            string saves=Path.Combine(temp,"中文 [Mac] & #; O'Brien","StoneShard"),backups=Path.Combine(temp,"备份 [1] & #; O'Brien");
            string slot=Path.Combine(saves,"characters_v1","character_3","exitsave_1"),data=Path.Combine(slot,"data.sav");Directory.CreateDirectory(slot);File.WriteAllText(data,"ALIVE_01");
            string hidden=Path.Combine(saves,".隐藏 [1].sav");File.WriteAllText(hidden,"hidden");File.SetAttributes(hidden,FileAttributes.Hidden);
            string longFile=Path.Combine(slot,new string('a',95),new string('b',95),"long.sav");Directory.CreateDirectory(Path.GetDirectoryName(longFile)!);File.WriteAllText(longFile,"LONG_PATH");
            var service=new SaveManagerService(workerPath,saves,backups);
            var archive=await service.RunAsync(SaveOperation.Backup);
            Check(archive.Files==3&&File.Exists(archive.Archive),"backup worker supports literal Chinese, brackets, apostrophes, spaces and semicolons");
            var engine=new SaveEngine(saves,backups);
            Refused(()=>new SaveEngine(saves,Path.GetPathRoot(saves)!),"互相包含");
            Refused(()=>new SaveEngine(saves,backups,stagingRoot:Path.Combine(saves,"work")),"不能位于存档目录");
            Check((await service.ListAsync()).Count==service.List().Count,"background archive listing returns the same verified folder listing");
            string emptySidecar=Path.Combine(backups,"Stoneshard-empty-hash.zip");File.Copy(archive.Archive,emptySidecar);File.WriteAllText(emptySidecar+".sha256","");
            Refused(()=>engine.Run(SaveOperation.Restore,emptySidecar),"校验失败");
            foreach(string bad in new[]{"../escape.sav","/absolute.sav","StoneShard/file.sav:stream","StoneShard/CON.sav","StoneShard/name. /bad.sav","StoneShard/../escape.sav"}){
                string path=Path.Combine(backups,"Stoneshard-unsafe.zip");if(File.Exists(path))File.Delete(path);
                using(var zip=ZipFile.Open(path,ZipArchiveMode.Create))zip.CreateEntry(bad);
                Refused(()=>engine.Run(SaveOperation.Restore,path),"不安全的路径");
            }
            string duplicate=Path.Combine(backups,"Stoneshard-duplicate.zip");
            using(var zip=ZipFile.Open(duplicate,ZipArchiveMode.Create)){zip.CreateEntry("StoneShard/data.sav");zip.CreateEntry("stoneshard/DATA.sav");}
            Refused(()=>engine.Run(SaveOperation.Restore,duplicate),"不安全的路径");
            string symbolic=Path.Combine(backups,"Stoneshard-link.zip");
            using(var zip=ZipFile.Open(symbolic,ZipArchiveMode.Create))zip.CreateEntry("StoneShard/link").ExternalAttributes=unchecked((int)0xA1FF0000);
            Refused(()=>engine.Run(SaveOperation.Restore,symbolic),"不安全的路径");
            Check(File.ReadAllText(data)=="ALIVE_01"&&!File.Exists(Path.Combine(temp,"escape.sav")),"invalid ZIPs leave the save and outside paths intact");
            Refused(()=>new SaveEngine(saves,Path.Combine(saves,"backups")),"互相包含");
            string missing=Path.Combine(temp,"missing","StoneShard");
            Refused(()=>new SaveEngine(missing,backups).Run(SaveOperation.Backup),"找不到");
            // A writer changes content while preserving size and timestamp.
            DateTime stamp=File.GetLastWriteTimeUtc(data);Task? writer=null;
            var changing=new SaveEngine(saves,backups,message=>{
                if(message.StartsWith("正在确认")&&writer is null)writer=Task.Run(async()=>{await Task.Delay(300);File.WriteAllText(data,"CHANGED!");File.SetLastWriteTimeUtc(data,stamp);});
            });
            try{Refused(()=>changing.Run(SaveOperation.Restore,archive.Archive),"仍在写入");}finally{if(writer is not null)await writer;}
            Check(File.ReadAllText(data)=="CHANGED!","concurrent same-size same-time writer is preserved, not overwritten");
            string legacy=Path.Combine(backups,"Stoneshard-legacy-rootless.zip");
            using(var zip=ZipFile.Open(legacy,ZipArchiveMode.Create))using(var text=new StreamWriter(zip.CreateEntry("characters_v1/character_3/exitsave_1/data.sav").Open()))text.Write("LEGACY_1");
            var result=await service.RunAsync(SaveOperation.Restore,legacy);
            Check(File.ReadAllText(data)=="LEGACY_1"&&result.SafetyArchive is not null,"rootless legacy ZIP without a sidecar restores with a safety archive");
            // Remove only this fixture's tree to represent a deleted game save.
            foreach(string path in Directory.EnumerateFiles(saves,"*",SearchOption.AllDirectories))File.SetAttributes(path,FileAttributes.Normal);
            if(!Path.GetFullPath(saves).StartsWith(temp+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe fixture target");
            Directory.Delete(saves,true);
            var recovered=await service.RunAsync(SaveOperation.Restore,archive.Archive);
            Check(File.ReadAllText(data)=="ALIVE_01"&&recovered.SafetyArchive is null,"deleted save directory is recovered without claiming a nonexistent safety copy");
            Check(longFile.Length>260&&File.ReadAllText(longFile)=="LONG_PATH","backup and restore retain a save path longer than 260 characters");
            // Hold a live directory handle without delete sharing. File replacement
            // must preserve this directory instead of renaming the save root.
            using(var handle=OpenDirectory(saves)){
                File.WriteAllText(data,"DEAD__01");await service.RunAsync(SaveOperation.Restore,archive.Archive);
                Check(!handle.IsInvalid&&File.ReadAllText(data)=="ALIVE_01","restore preserves an open save-directory handle");
            }
            Check(!Directory.EnumerateDirectories(backups,".work-*").Any()&&!Directory.EnumerateDirectories(Path.GetDirectoryName(saves)!,".StoneShard-restore-*").Any(),"successful and rejected operations clean only their own work directories");
            string transferBackups=Path.Combine(temp,"transfer destination"),staging=Path.Combine(temp,"local staging");
            var transfer=new SaveEngine(saves,transferBackups,stagingRoot:staging);
            transfer.Run(SaveOperation.Latest);
            string latestZip=Path.Combine(transferBackups,"Stoneshard-latest.zip");
            byte[] previousZip=File.ReadAllBytes(latestZip),previousHash=File.ReadAllBytes(latestZip+".sha256");
            int historyCount=Directory.GetFiles(transferBackups,"Stoneshard*.zip").Length;
            var interrupted=new SaveEngine(saves,transferBackups,message=>{
                if(message.StartsWith("正在写入备份目录")){
                    Check(Directory.EnumerateFiles(staging,"*.zip",SearchOption.AllDirectories).Any()&&!Directory.EnumerateDirectories(transferBackups,"snapshot-*",SearchOption.AllDirectories).Any(),"compression and verification finish locally before transfer");
                    string work=Directory.GetDirectories(transferBackups,".work-*").Single();Directory.CreateDirectory(Path.Combine(work,"upload.zip"));
                }
            },staging);
            try{interrupted.Run(SaveOperation.Latest);throw new Exception("Expected transfer failure");}
            catch(SaveTransferException e){
                string checksum=File.ReadAllText(e.LocalArchive+".sha256").Split(' ')[0];
                Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(e.LocalArchive)))==checksum,"interrupted transfer retains a complete verified local ZIP and checksum");
                Check(Directory.GetFiles(Path.GetDirectoryName(e.LocalArchive)!,"*",SearchOption.AllDirectories).Length==2,"retained recovery contains only the completed archive and checksum");
            }
            Check(Directory.GetFiles(transferBackups,"Stoneshard*.zip").Length==historyCount&&File.ReadAllBytes(latestZip).SequenceEqual(previousZip)&&File.ReadAllBytes(latestZip+".sha256").SequenceEqual(previousHash),"failed upload publishes no partial history and preserves the old latest pair");
            using(var locked=new FileStream(latestZip+".sha256",FileMode.Open,FileAccess.Read,FileShare.Read)){
                try{transfer.Run(SaveOperation.Latest);throw new Exception("Expected locked checksum failure");}
                catch(SaveTransferException e){Check(File.Exists(e.LocalArchive)&&File.ReadAllBytes(latestZip).SequenceEqual(previousZip)&&File.ReadAllBytes(latestZip+".sha256").SequenceEqual(previousHash),"locked latest checksum preserves both old files and a local recovery archive");}
            }
            Check(File.ReadAllText(data)=="ALIVE_01","transfer and latest-update failures never modify the game save");
            Console.WriteLine($"Save compatibility: {passed} checks passed");
        }finally{
            string full=Path.GetFullPath(temp);if(Path.GetDirectoryName(full)!=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))||!Path.GetFileName(full).StartsWith("Stoneshard-compat-"))throw new Exception("Unsafe compatibility cleanup");
            foreach(string path in Directory.EnumerateFiles(full,"*",SearchOption.AllDirectories))File.SetAttributes(path,FileAttributes.Normal);
            Directory.Delete(full,true);
        }
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name,uint access,uint sharing,nint security,uint disposition,uint flags,nint template);
    private static Microsoft.Win32.SafeHandles.SafeFileHandle OpenDirectory(string path)
    {
        var handle=CreateFile(path,0,3,0,3,0x02000000,0);if(handle.IsInvalid)throw new System.ComponentModel.Win32Exception();return handle;
    }
}
