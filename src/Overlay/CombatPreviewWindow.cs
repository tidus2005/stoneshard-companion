using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Forms=System.Windows.Forms;
namespace StoneshardCompanion;

public sealed class CombatPreviewWindow : Window
{
    private readonly Border surface=new(){Background=new SolidColorBrush(Color.FromArgb(244,22,25,31)),BorderBrush=new SolidColorBrush(Color.FromRgb(91,100,115)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(14)};
    private readonly Viewbox fit=new(){Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly};
    private long renderedAt;private bool renderedDetails;
    public CombatPreviewWindow(){
        Title="战斗预览 · 晶石助手";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
        ShowInTaskbar=false;ShowActivated=false;Topmost=true;Focusable=false;IsHitTestVisible=false;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=12;Foreground=Brushes.Gainsboro;
        Content=fit;fit.Child=surface;
        SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowLongPtr(h,-20,Native.GetWindowLongPtr(h,-20)|0x080000A0);HwndSource.FromHwnd(h)?.AddHook(Hook);};
    }
    private nint Hook(nint h,int msg,nint wp,nint lp,ref bool handled){if(msg==0x84){handled=true;return -1;}if(msg==0x21){handled=true;return 3;}return 0;}
    public void Render(CombatTelemetry data,CharacterTelemetry player,bool details){
        var root=new StackPanel();surface.Child=root;surface.Width=930;
        var header=new DockPanel{Margin=new Thickness(0,0,0,10)};root.Children.Add(header);
        var hint=new TextBlock{Text="Alt 展开 · Ctrl+Alt+I 完整数据",Foreground=Brushes.DarkGray,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(hint,Dock.Right);header.Children.Add(hint);
        header.Children.Add(new TextBlock{Text=$"{StatMechanics.CleanLabel(data.Name)}  ·  {CombatPreview.Format(data.Distance)} 格",FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,TextTrimming=TextTrimming.CharacterEllipsis});
        var grid=new Grid();root.Children.Add(grid);
        var sections=CombatPreview.Describe(data,player,details);
        for(int col=0;col<sections.Length;col++){
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            var section=sections[col];var stack=new StackPanel{Margin=new Thickness(col==0?0:13,0,col==2?0:13,0)};Grid.SetColumn(stack,col);grid.Children.Add(stack);
            stack.Children.Add(new TextBlock{Text=section.Title,FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(216,187,125)),Margin=new Thickness(0,0,0,8)});
            foreach(var row in section.Rows){
                var line=new Grid{Margin=new Thickness(0,0,0,4)};line.ColumnDefinitions.Add(new(){Width=new GridLength(105)});line.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
                line.Children.Add(new TextBlock{Text=row.Label,Foreground=Brushes.Gray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,7,0)});
                var value=new TextBlock{Text=row.Value,TextWrapping=TextWrapping.Wrap,Foreground=row.Highlight?Brushes.PaleGreen:Brushes.Gainsboro,FontWeight=row.Highlight?FontWeights.SemiBold:FontWeights.Normal};Grid.SetColumn(value,1);line.Children.Add(value);stack.Children.Add(line);
            }
        }
        root.Children.Add(new TextBlock{Text="≈ 为普通近战模型估算；未含技能条件、远程遮挡与受击部位。— 表示未读取，不能当成 0。",Foreground=Brushes.DarkGray,FontSize=11,Margin=new Thickness(0,10,0,0),TextWrapping=TextWrapping.Wrap});
        surface.Measure(new Size(930,double.PositiveInfinity));
    }
    public void Update(CombatTelemetry data,CharacterTelemetry player,ulong scene,bool allowed){
        if(!allowed||!data.Fresh(Environment.TickCount64,scene)||!Native.GetCursorPos(out var cursor)||Math.Abs(cursor.X-data.CursorX)>10||Math.Abs(cursor.Y-data.CursorY)>10){Hide();return;}
        bool details=(Native.GetAsyncKeyState(0x12)&0x8000)!=0;
        if(renderedAt!=data.At||renderedDetails!=details){Render(data,player,details);renderedAt=data.At;renderedDetails=details;}
        var work=Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X,cursor.Y)).WorkingArea;
        var h=new WindowInteropHelper(this).EnsureHandle();double dpi=Math.Max(96,Native.GetDpiForWindow(h))/96.0;
        double scale=Math.Min(1,Math.Min((work.Width-24)/(930*dpi),(work.Height-48)/(surface.DesiredSize.Height*dpi)));
        Width=930*scale;Height=surface.DesiredSize.Height*scale;
        int width=(int)Math.Ceiling(Width*dpi),height=(int)Math.Ceiling(Height*dpi);
        var p=CombatPreview.Place(cursor.X,cursor.Y,width,height,work.Left+8,work.Top+8,work.Right-8,work.Bottom-8);
        if(!IsVisible)Show();Native.SetWindowPos(h,-1,(int)p.X,(int)p.Y,width,height,0x10);
    }
}
