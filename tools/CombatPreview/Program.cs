using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StoneshardCompanion;
internal static class Program
{
    [STAThread] static void Main(string[] args){
        var app=new Application();RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
        string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
        var s=new CombatTelemetry{At=1000,Scene=1,TargetId=123,Name="模拟目标 · 重甲劫匪",Distance=1,
            Player=new(){["Hit_Chance"]=110,["FMB"]=8,["CRT"]=19,["DMG"]=48,["Armor_Piercing"]=25,["Armor_Damage"]=140,["EVS"]=12,["PRR"]=28,["Block_Power"]=34,["Crit_Avoid"]=20,["Physical_Resistance"]=15,["is_range"]=0},
            Target=new(){["HP"]=126,["max_hp"]=180,["MP"]=55,["max_mp"]=75,["DEF"]=18,["ArmorDurability"]=72,["Hit_Chance"]=95,["FMB"]=15,["EVS"]=10,["PRR"]=35,["Block_Power"]=26,["CTA"]=20,["DMG"]=36,["Armor_Piercing"]=10,["Armor_Damage"]=115,["Physical_Resistance"]=12,["Magic_Resistance"]=5,["Nature_Resistance"]=0,["Stun_Resistance"]=30,["Bleeding_Resistance"]=20,["is_range"]=0,["is_mage"]=0},
            Buffs=[new("失衡"),new("流血"),new("左腿受伤")],BuffsComplete=true};
        var c=new CharacterTelemetry{At=1000,SourcesAvailable=true,Sources=[new("Hit_Chance",10,1,"战斗专注"),new("FMB",-5,2,"稳定姿态"),new("CRT",5,3,"锐利目光")]};
        var window=new CombatPreviewWindow();Export("normal",false);Export("details",true);
        s.SkillSelected=true;s.SkillName="冲撞";Export("skill-unknown",false);
        s.Name="模拟 · 不完整数据";s.Target.Clear();s.BuffsComplete=false;s.Buffs=[];Export("missing",false);
        Console.WriteLine("Rendered four offscreen combat panels; no game or personal preferences accessed.");
        void Export(string name,bool details){
            window.Render(s,c,details);var content=(FrameworkElement)((Viewbox)window.Content).Child;
            content.Measure(new Size(930,double.PositiveInfinity));var size=content.DesiredSize;content.Arrange(new Rect(size));content.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(size.Width),(int)Math.Ceiling(size.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(content);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,name+".png"));png.Save(file);
            if(size.Width!=930||size.Height>1400)throw new Exception("Unexpected layout size: "+size);
        }
    }
}
