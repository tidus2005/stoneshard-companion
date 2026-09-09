using StoneshardCompanion;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;

internal static class UpdateChecks
{
    private static int passed;
    private static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    private static void Refused(Action action,string name){try{action();}catch(Exception e) when(e is IOException or UnauthorizedAccessException){Check(true,name);return;}throw new Exception("Expected refusal: "+name);}
    public static async Task Run()
    {
        string temp=Path.Combine(Path.GetTempPath(),"Stoneshard-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try{
            string name="StoneshardCompanion-0.4.0-win-x64.zip",url=$"https://github.com/{ReleaseClient.Repository}/releases/download/v0.4.0/";
            string Json(bool preview=false,string download="")=>JsonSerializer.Serialize(new{draft=false,prerelease=preview,tag_name="v0.4.0",assets=new[]{new{name,browser_download_url=download==""?url+name:download,size=1},new{name=name+".sha256",browser_download_url=url+name+".sha256",size=1}}});
            Check(ReleaseClient.Parse(Json(),new Version(0,3,11))?.Version==new Version(0,4,0),"numeric release upgrade selected");
            Check(ReleaseClient.Parse(Json(),new Version(0,4,0)) is null&&ReleaseClient.Parse(Json(),new Version(0,5,0)) is null,"same version and downgrade skipped");
            Check(ReleaseClient.Parse(Json(true),new Version(0,3,0)) is null,"prereleases skipped");
            Refused(()=>ReleaseClient.Parse(Json(download:"https://example.com/app.zip"),new Version(0,3,0)),"foreign update URL rejected");
            string source=Path.Combine(temp,"source");Directory.CreateDirectory(source);
            string[] names=["StoneshardCompanion.exe","StoneshardBridge.dll","Start.cmd","LICENSE","THIRD_PARTY_NOTICES.md","使用说明.md","Assets/Icons/ATTRIBUTION.md"];
            var hashes=new Dictionary<string,string>();
            foreach(string file in names){string path=Path.Combine(source,file);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,"NEW_"+file);hashes[file]=UpdatePackage.Hash(path);}
            File.WriteAllText(Path.Combine(source,"SHA256.json"),JsonSerializer.Serialize(hashes));
            string zip=Path.Combine(temp,"release.zip");ZipFile.CreateFromDirectory(source,zip,CompressionLevel.Fastest,true);
            string package=UpdatePackage.Extract(zip,Path.Combine(temp,"package"));
            Check(UpdatePackage.Validate(package).Count==8,"archive and every manifest hash verified");
            string invalid=Path.Combine(temp,"invalid.zip");
            using(var archive=ZipFile.Open(invalid,ZipArchiveMode.Create))archive.CreateEntry("source/../escaped.txt");
            Refused(()=>UpdatePackage.Extract(invalid,Path.Combine(temp,"invalid")),"archive traversal rejected before extraction");
            Check(!File.Exists(Path.Combine(temp,"escaped.txt")),"outside files untouched");
            string target=Path.Combine(temp,"target");Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target,UpdatePackage.Executable),"OLD");File.WriteAllText(Path.Combine(target,"personal.txt"),"KEEP");
            UpdatePackage.Install(package,target,Path.Combine(temp,"rollback-success"));
            Check(File.ReadAllText(Path.Combine(target,UpdatePackage.Executable)).StartsWith("NEW_")&&File.ReadAllText(Path.Combine(target,"personal.txt"))=="KEEP","install updates app and preserves unrelated files");
            Check(File.ReadAllText(Path.Combine(temp,"rollback-success",UpdatePackage.Executable))=="OLD","previous executable retained for recovery");
            string failed=Path.Combine(temp,"failed");Directory.CreateDirectory(failed);File.WriteAllText(Path.Combine(failed,UpdatePackage.Executable),"OLD");Directory.CreateDirectory(Path.Combine(failed,"LICENSE"));
            Refused(()=>UpdatePackage.Install(package,failed,Path.Combine(temp,"rollback-failure")),"mid-install failure reported");
            Check(File.ReadAllText(Path.Combine(failed,UpdatePackage.Executable))=="OLD"&&!File.Exists(Path.Combine(failed,"StoneshardBridge.dll")),"mid-install failure rolls back replaced and newly introduced files");
            File.AppendAllText(Path.Combine(package,UpdatePackage.Executable),"CORRUPT");
            Refused(()=>UpdatePackage.Install(package,target,Path.Combine(temp,"bad-hash")),"tampered staged package rejected before replacing files");
            byte[] bytes=File.ReadAllBytes(zip);
            using var http=new HttpClient(new FakeHandler(bytes,UpdatePackage.Hash(zip),name));
            var release=new ReleaseUpdate(new Version(0,4,0),name,new Uri(url+name),new Uri(url+name+".sha256"),bytes.Length);
            var downloaded=await new ReleaseClient(http).DownloadAsync(release,Path.Combine(temp,"download"));
            Check(UpdatePackage.Validate(downloaded).Count==8,"HTTP download length, checksum and extraction verified");
            using var corrupt=new HttpClient(new FakeHandler(bytes,new string('0',64),name));
            try{await new ReleaseClient(corrupt).DownloadAsync(release,Path.Combine(temp,"corrupt"));throw new Exception("Bad download accepted");}catch(IOException){Check(true,"corrupt download rejected");}
            Console.WriteLine($"Update checks: {passed} passed");
        }finally{
            string path=Path.GetFullPath(temp);if(Path.GetDirectoryName(path)!=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))||!Path.GetFileName(path).StartsWith("Stoneshard-update-"))throw new Exception("Unsafe update fixture cleanup");
            Directory.Delete(path,true);
        }
    }
    private sealed class FakeHandler(byte[] bytes,string hash,string name):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=request.RequestUri!.AbsolutePath.EndsWith(".sha256")?new StringContent(hash+"  "+name):new ByteArrayContent(bytes)});
    }
}
