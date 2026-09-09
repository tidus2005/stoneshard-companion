using StoneshardCompanion;
using System.IO.Compression;
using System.Text;
internal static class PreviewChecks
{
    public static void Run(){int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
        string file=Path.Combine(Path.GetTempPath(),"stoneshard-preview-"+Guid.NewGuid()+".zip");
        try{
            void Create(bool corrupt=false){using var z=ZipFile.Open(file,ZipArchiveMode.Create);
                void Map(string name,string json){using var s=z.CreateEntry(name).Open();using var c=new ZLibStream(s,CompressionLevel.Fastest);c.Write(Encoding.UTF8.GetBytes(json+"CHECKSUM"));}
                Map("StoneShard/characters_v1/characters.map",corrupt?"[]":"{\"lastCharacter\":\"character_3\"}");
                foreach(var (slot,date,value) in new[]{("character_1/exitsave",46002,1),("character_3/exitsave",46001,3),("character_3/save_1",46000,4)}){
                    Map("StoneShard/characters_v1/"+slot+"/save.map",$"{{\"dateTime\":{date}}}");
                    using var s=z.CreateEntry("StoneShard/characters_v1/"+slot+"/preview.png").Open();s.WriteByte((byte)value);
                }
            }
            Create();var p=SavePreviews.ReadLatest(file);
            Check(p?.Image.Single()==3&&p.Slot.Contains("character_3/exitsave"),"latest save of active character selected despite another newer character");
            Check(p!.SavedAt.UtcDateTime==DateTime.SpecifyKind(DateTime.FromOADate(46001),DateTimeKind.Utc),"embedded save timestamp preferred over zip entry time");
            File.Delete(file);Create(true);Check(SavePreviews.ReadLatest(file)?.Image.Single()==1,"malformed character metadata safely falls back to latest save");
            File.Delete(file);using(var zip=ZipFile.Open(file,ZipArchiveMode.Create)){using var s=zip.CreateEntry("empty.txt").Open();}
            Check(SavePreviews.ReadLatest(file) is null,"archives without preview supported");
            Console.WriteLine($"{passed} save preview checks passed. Only temporary archives accessed.");
        }finally{File.Delete(file);}
    }
}
