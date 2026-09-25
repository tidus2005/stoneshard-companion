using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace StoneshardCompanion;

// Reads the user's installed game assets on demand; no game artwork is bundled.
internal sealed class GameBuildAssets
{
    internal sealed record Entry(string Name,string Description,BitmapSource? Icon);
    private readonly Dictionary<string,Entry> entries=new(StringComparer.Ordinal);
    public Entry Get(string key)=>entries.GetValueOrDefault(key)??new(key.Replace("o_pass_skill_","").Replace("o_skill_","").Replace("_ico","").Replace('_',' '),"",null);
    public static GameBuildAssets Load(IEnumerable<string> keys){
        var result=new GameBuildAssets();
        string? exe=GameSession.Discover().FirstOrDefault()?.Path;
        exe??=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam","steamapps","common","Stoneshard","StoneShard.exe")}.FirstOrDefault(File.Exists);
        if(exe is null)return result;
        var lookup=keys.ToDictionary(k=>k,k=>k.Replace("o_pass_skill_","").Replace("o_skill_","").Replace("_ico",""),StringComparer.Ordinal);
        var wanted=lookup.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var titles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var descriptions=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        byte[] binary=File.ReadAllBytes(exe);
        for(int at=0;at<binary.Length;){
            int end=Array.IndexOf(binary,(byte)0,at);if(end<0)break;int length=end-at;
            if(length>10&&length<100000&&((binary[at]>=65&&binary[at]<=90)||(binary[at]>=97&&binary[at]<=122))){
                int semi=Array.IndexOf(binary,(byte)';',at,Math.Min(length,90));
                if(semi>at){string key=Encoding.ASCII.GetString(binary,at,semi-at);
                    if(wanted.Contains(key)){var columns=Encoding.UTF8.GetString(binary,at,length).Split(';');
                        if(columns.Length>=12&&Regex.IsMatch(columns[3],"[\u4e00-\u9fff]")){
                            string text=Regex.Replace(columns[3],"~[^~]*~","").Replace('#','\n');text=Regex.Replace(text,@"/\*.*?\*/","…");
                            if(columns[3].Length<45&&!columns[3].Contains('#'))titles[key]=text;else descriptions[key]=text;
                        }
                    }
                }
            }at=end+1;
        }
        foreach(var pair in lookup)result.entries[pair.Key]=new(titles.GetValueOrDefault(pair.Value)??result.Get(pair.Key).Name,descriptions.GetValueOrDefault(pair.Value)??"",null);
        try{
            byte[] data=File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(exe)!,"data.win"));
            int U(int p){if(p<0||p>data.Length-4)throw new InvalidDataException();return checked((int)BitConverter.ToUInt32(data,p));}
            int H(int p)=>BitConverter.ToUInt16(data,p);
            string S(int p){int end=Array.IndexOf(data,(byte)0,p);if(end<0||end-p>512)throw new InvalidDataException();return Encoding.UTF8.GetString(data,p,end-p);}
            var chunks=new Dictionary<string,int>();for(int p=8;p+8<data.Length;){chunks[Encoding.ASCII.GetString(data,p,4)]=p+8;p=checked(p+8+U(p+4));}
            int objects=chunks["OBJT"],sprites=chunks["SPRT"],textures=chunks["TXTR"];
            var pages=new Dictionary<int,BitmapSource>();
            for(int i=0;i<U(objects);i++){
                int obj=U(objects+4+i*4);string key=S(U(obj));if(!result.entries.ContainsKey(key))continue;
                int sprite=BitConverter.ToInt32(data,obj+4);if(sprite<0||sprite>=U(sprites))continue;int sp=U(sprites+4+4*sprite);
                if(BitConverter.ToInt32(data,sp+56)!=-1||U(sp+60)!=3||U(sp+64)!=0)continue;
                int count=U(sp+84);if(count<1||count>20)continue;int frame=U(sp+88+(count>1?4:0));
                int page=H(frame+20);if(page>=U(textures))continue;
                if(!pages.TryGetValue(page,out var bitmap)){
                    int texture=U(textures+4+4*page),png=U(texture+24),end=png+8;
                    if(data.AsSpan(png,4).SequenceEqual("2zoq"u8)||data.AsSpan(png,4).SequenceEqual("fioq"u8)){bitmap=GameTexture.Decode(data,png);pages[page]=bitmap;}
                    else{
                    if(!data.AsSpan(png,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))continue;
                    while(end+12<data.Length){int size=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(end,4));if(size<0)throw new InvalidDataException();bool last=Encoding.ASCII.GetString(data,end+4,4)=="IEND";end=checked(end+12+size);if(last)break;}
                    using var stream=new MemoryStream(data,png,end-png,false);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();bitmap=image;pages[page]=bitmap;
                    }
                }
                var crop=new CroppedBitmap(bitmap,new Int32Rect(H(frame),H(frame+2),H(frame+4),H(frame+6)));crop.Freeze();result.entries[key]=result.entries[key] with{Icon=crop};
            }
        }catch(Exception e) when(e is IOException or ArgumentException or OverflowException or NotSupportedException){UserPreferences.Log("build-icons "+e.Message);}
        return result;
    }
    public static Dictionary<string,string> Groups(IEnumerable<BuildSkill> skills){
        var starts=new Dictionary<string,string>{["vivifying_violence"]="运动",["beyond_the_veil"]="秘术",["rune_of_power"]="地术",["recharge"]="电术",["pyromania"]="火术",["preparation"]="战斗",["magic_lore"]="法术精研",["halt"]="生存",["unstoppable"]="双持",["armor_adjustment"]="披甲战斗",["torch_strike"]="基础动作",["battle_trance"]="长杖",["ultimate_resilience"]="盾牌",["upper_hand"]="远程兵器",["wounding_spearhead"]="长枪",["heavy_concussion"]="双手钝器",["fatal_hit"]="双手斧",["revanche"]="双手剑",["dance_of_death"]="短刀",["concussion"]="单手钝器",["show_no_mercy"]="单手斧",["sharpened_edge"]="单手剑"};
        var result=new Dictionary<string,string>();string group="其他";
        foreach(var skill in skills){string key=skill.Key.Replace("o_pass_skill_","").Replace("o_skill_","").Replace("_ico","");if(starts.TryGetValue(key,out var next))group=next;result[skill.Key]=skill.Fixed?"基础动作":group;}return result;
    }
}
