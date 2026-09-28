using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using StoneshardCompanion;

internal static class Program
{
    [STAThread] static void Main(string[] args){
        var app=new Application();RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
        string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);int checks=0;
        void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
        var snapshot=new LiveBuildSnapshot("Jorgrim","0123456789abcdef",0,[25,11,15,17,10,0,0,24],[11,10,11,11,10],[],2,37,1000000372,[5,1,1,0,1,1,0,0,3,2,3,0,0,0],true,"CaravanCamp",["","","","",""]);
        var window=new GemRefundWindow(snapshot,"力量");Export(window,"empty.png");
        Button Button(string label)=>Descendants((DependencyObject)window.Content).OfType<Button>().Single(b=>Equals(b.Content,label));
        TextBox Quantity(string gem)=>Descendants((DependencyObject)window.Content).OfType<TextBox>().Single(q=>AutomationProperties.GetName(q)==gem+"消耗数量");
        void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(!Button("使用这些宝石").IsEnabled,"Empty selection must not apply");
        Click(Button("自动凑够 600"));Check(Button("使用这些宝石").IsEnabled,"Auto selection should reach threshold");Export(window,"mixed-600.png");
        Check(Descendants((DependencyObject)window.Content).OfType<TextBlock>().Any(t=>t.Text=="已选价值 600 / 600"),"Auto selection should minimize value");
        foreach(var q in Descendants((DependencyObject)window.Content).OfType<TextBox>())q.Text="0";
        Quantity("翡翠").Text="4";Check(!Button("使用这些宝石").IsEnabled,"500 must be rejected");Quantity("翡翠").Text="5";Check(Button("使用这些宝石").IsEnabled,"625 should be allowed");
        Check(Descendants((DependencyObject)window.Content).OfType<TextBlock>().Any(t=>t.Text.Contains("超出 25")&&t.Text.Contains("不找零")),"Excess must be explicit");Export(window,"excess-625.png");
        foreach(string invalid in new[]{"6","-1","1.5","abc","2147483648",""}){Quantity("翡翠").Text=invalid;Check(!Button("使用这些宝石").IsEnabled,"Invalid quantity must disable apply: "+invalid);}
        Quantity("翡翠").Text="0";Quantity("钻石").Text="1";Check(!Button("使用这些宝石").IsEnabled,"Missing diamond cannot be spent");Export(window,"invalid.png");
        Check(window.SelectedMaterials is null,"Editing a preview must never submit a transaction");
        var poor=new GemRefundWindow(snapshot with{Gems=new int[LiveBuild.GemKeys.Length]},"英勇冲锋");Export(poor,"insufficient.png");
        Check(!Descendants((DependencyObject)poor.Content).OfType<Button>().Single(b=>Equals(b.Content,"自动凑够 600")).IsEnabled,"Unavailable suggestion must be disabled");
        var noCredit=new GemRefundWindow(snapshot with{Credits=0},"力量");Export(noCredit,"no-credit.png");
        Click(Descendants((DependencyObject)noCredit.Content).OfType<Button>().Single(b=>Equals(b.Content,"自动凑够 600")));
        Check(!Descendants((DependencyObject)noCredit.Content).OfType<Button>().Single(b=>Equals(b.Content,"使用这些宝石")).IsEnabled,"Credit still required");
        Console.WriteLine($"PASS {checks} gem selection UI checks. Offscreen only; no game, personal settings or saves accessed.");
        void Export(Window target,string file){
            var content=(FrameworkElement)target.Content;double width=target.Width-20,height=target.Height-40;content.Width=width;content.Height=height;content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
            var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){dc.DrawRectangle(target.Background,null,new Rect(0,0,width,height));dc.DrawRectangle(new VisualBrush(content),null,new Rect(0,0,width,height));}
            var bitmap=new RenderTargetBitmap((int)width,(int)height,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(output,file));png.Save(stream);
        }
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;}
}
