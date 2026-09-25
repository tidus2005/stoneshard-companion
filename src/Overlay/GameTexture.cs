using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.SharpZipLib.BZip2;

namespace StoneshardCompanion;

internal static class GameTexture
{
    // GameMaker's little-endian QOI layout, with optional BZip2 wrapper.
    // Format reference: UnderminersTeam/UndertaleModTool, Util/QoiConverter.cs.
    public static BitmapSource Decode(byte[] file,int start){
        byte[] qoi;
        if(file.AsSpan(start,4).SequenceEqual("2zoq"u8)){
            int expected=BitConverter.ToInt32(file,start+8);if(expected<12||expected>90_000_000)throw new InvalidDataException("Texture size");
            using var source=new MemoryStream(file,start+12,file.Length-start-12,false);using var decoder=new BZip2InputStream(source);
            qoi=new byte[expected];decoder.ReadExactly(qoi);if(decoder.ReadByte()!=-1)throw new InvalidDataException("Texture length");
        }else qoi=file.AsSpan(start).ToArray();
        if(!qoi.AsSpan(0,4).SequenceEqual("fioq"u8))throw new InvalidDataException("Texture format");
        int width=BitConverter.ToUInt16(qoi,4),height=BitConverter.ToUInt16(qoi,6),length=BitConverter.ToInt32(qoi,8);
        if(width<1||height<1||width>4096||height>4096||length<1||length>qoi.Length-12)throw new InvalidDataException("Texture dimensions");
        byte[] pixels=new byte[checked(width*height*4)];byte[][] cache=Enumerable.Range(0,64).Select(_=>new byte[4]).ToArray();
        byte[] color=[0,0,0,255];int at=12,run=0;
        int Next(){if(at>=length+12)throw new InvalidDataException("Truncated texture");return qoi[at++];}
        static int Signed(int value,int bits){int mask=(1<<bits)-1,sign=1<<(bits-1);return ((value&mask)^sign)-sign;}
        void Delta(int channel,int delta)=>color[channel]=unchecked((byte)(color[channel]+delta));
        for(int pixel=0;pixel<pixels.Length;pixel+=4){
            if(run>0)run--;
            else{
                int tag=Next();
                if(tag<64)Array.Copy(cache[tag],color,4);
                else if(tag<96)run=tag&31;
                else if(tag<128)run=((tag&31)<<8|Next())+32;
                else if(tag<192){Delta(0,Signed(tag>>4,2));Delta(1,Signed(tag>>2,2));Delta(2,Signed(tag,2));}
                else if(tag<224){int bits=(tag<<8)|Next();Delta(0,Signed(bits>>8,5));Delta(1,Signed(bits>>4,4));Delta(2,Signed(bits,4));}
                else if(tag<240){int bits=(tag<<16)|(Next()<<8)|Next();for(int channel=0;channel<4;channel++)Delta(channel,Signed(bits>>(15-channel*5),5));}
                else{for(int channel=0;channel<4;channel++)if((tag&(8>>channel))!=0)color[channel]=(byte)Next();}
                Array.Copy(color,cache[(color[0]^color[1]^color[2]^color[3])&63],4);
            }
            pixels[pixel]=color[2];pixels[pixel+1]=color[1];pixels[pixel+2]=color[0];pixels[pixel+3]=color[3];
        }
        var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);bitmap.Freeze();return bitmap;
    }
}
