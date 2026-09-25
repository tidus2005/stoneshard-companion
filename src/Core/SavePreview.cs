using System.IO.Compression;
using System.Text.Json;
namespace StoneshardCompanion;

public sealed record SavePreview(string Slot,DateTimeOffset SavedAt,byte[] Image);
public static class SavePreviews
{
    // Read the image already saved by the game, never a desktop screenshot.
    // Bounded extraction to memory; no archive paths are written to disk.
    public static SavePreview? ReadLatest(string archive){
        using var zip=ZipFile.OpenRead(archive);
        string? character=null;
        using(var index=ReadMap(zip.GetEntry("StoneShard/characters_v1/characters.map"))){if(index?.RootElement.ValueKind==JsonValueKind.Object&&index.RootElement.TryGetProperty("lastCharacter",out var value)&&value.ValueKind==JsonValueKind.String)character=value.GetString();}
        var entry=zip.Entries.Where(e=>e.FullName.EndsWith("/preview.png",StringComparison.OrdinalIgnoreCase)&&e.Length>0&&e.Length<=4_000_000)
            .Select(e=>{double date=0;using var map=ReadMap(zip.GetEntry(e.FullName.Replace("preview.png","save.map")));if(map?.RootElement.ValueKind==JsonValueKind.Object&&map.RootElement.TryGetProperty("dateTime",out var time)&&time.ValueKind==JsonValueKind.Number)time.TryGetDouble(out date);return (Entry:e,Date:date);})
            .OrderByDescending(e=>character is not null&&e.Entry.FullName.Contains("/"+character+"/",StringComparison.Ordinal))
            .ThenByDescending(e=>e.Date).ThenByDescending(e=>e.Entry.LastWriteTime).FirstOrDefault();
        if(entry.Entry is null)return null;
        using var source=entry.Entry.Open();using var output=new MemoryStream();source.CopyTo(output);
        DateTimeOffset saved=entry.Entry.LastWriteTime;
        if(entry.Date is >0 and <2958465)saved=new DateTimeOffset(DateTime.SpecifyKind(DateTime.FromOADate(entry.Date),DateTimeKind.Utc));
        return new(entry.Entry.FullName.Replace("StoneShard/characters_v1/",""),saved,output.ToArray());
    }
    private static JsonDocument? ReadMap(ZipArchiveEntry? entry){
        if(entry is null||entry.Length>65536)return null;
        try{using var source=entry.Open();using var z=new ZLibStream(source,CompressionMode.Decompress);using var output=new MemoryStream();var buffer=new byte[4096];int n;
            while((n=z.Read(buffer))>0){if(output.Length+n>65536)return null;output.Write(buffer,0,n);}
            // GameMaker appends a checksum after the JSON document.
            var reader=new Utf8JsonReader(output.ToArray());return JsonDocument.ParseValue(ref reader);
        }catch(Exception ex) when(ex is IOException or JsonException){return null;}
    }
}
