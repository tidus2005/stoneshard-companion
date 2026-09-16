using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace StoneshardCompanion;
public sealed class StatsWindow:Window
{
    private readonly MainWindow owner;
    private readonly Border surface;
    private readonly WrapPanel rows=new();
    private readonly List<FrameworkElement> groups=[];
    private readonly Dictionary<string,double> baseline=[];
    private double? baselinePlayer;
    private string baselineAt="";
    private readonly TextBlock status=new(){Foreground=Brushes.Silver,FontSize=11,Margin=new Thickness(10)};
    private readonly Dictionary<string,(TextBlock Value,Border Row,Button Star)> controls=[];
    private CharacterTelemetry snapshot=new();
    private bool dragging;

    public StatsWindow(MainWindow parent){
        owner=parent;Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/StoneshardCompanion;component/PanelTheme.xaml",UriKind.Relative)});Title="晶石助手 · 角色属性";Width=290;Height=380;MinWidth=250;MinHeight=200;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;
        Topmost=true;ShowActivated=false;ShowInTaskbar=App.TestWindows;FontFamily=new FontFamily("Microsoft YaHei UI");Foreground=Brushes.Wheat;
        var shell=new Grid();Content=shell;var root=new DockPanel{Margin=new Thickness(5)};surface=new Border{BorderBrush=new SolidColorBrush(Color.FromRgb(112,98,71)),CornerRadius=new CornerRadius(8),Child=root};shell.Children.Add(surface);
        ApplyAppearance();
        var header=new DockPanel{Margin=new Thickness(10)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var close=new Button{Content="隐藏",Focusable=false};DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);close.Click+=(_,_)=>{owner.Preferences.ShowStats=false;owner.SavePreferences();Hide();};
        var title=new TextBlock{Text="角色属性",FontSize=17};header.Children.Add(title);
        title.MouseLeftButtonDown+=(_,e)=>{if(e.ClickCount==1){try{dragging=true;DragMove();owner.Preferences.StatsX=Left;owner.Preferences.StatsY=Top;owner.Preferences.StatsPlaced=true;owner.SavePreferences();}catch(InvalidOperationException){}finally{dragging=false;}}};
        var tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(6,0,6,4)};DockPanel.SetDock(tabs,Dock.Top);root.Children.Add(tabs);
        var all=new Button{Content="全部",Margin=new Thickness(3),Focusable=false};var favorites=new Button{Content="★ 特别关注",Margin=new Thickness(3),Focusable=false};tabs.Children.Add(all);tabs.Children.Add(favorites);
        all.Click+=(_,_)=>{owner.Preferences.StatsFavoritesOnly=false;owner.SavePreferences();Rebuild();};favorites.Click+=(_,_)=>{owner.Preferences.StatsFavoritesOnly=true;owner.SavePreferences();Rebuild();};
        var compare=new Button{Content="设为基线",Margin=new Thickness(3),ToolTip="把此刻的新鲜数值设为本次角色会话的对比基线。换角色、重新读档或重启助手后清除。"};tabs.Children.Add(compare);
        compare.Click+=(_,_)=>{if(!snapshot.Fresh(Environment.TickCount64))return;baseline.Clear();foreach(var st in snapshot.Stats)if(st.Value is double v)baseline[st.Key]=v;baselinePlayer=snapshot.Player;baselineAt=DateTime.Now.ToString("HH:mm:ss");Refresh(snapshot);};
        var legend=new TextBlock{Text="当前值（相对基线变化） · 绿：改善 / 红：变差 · 悬停看拆解",Foreground=Brushes.DarkKhaki,FontSize=10,Margin=new Thickness(10,0,8,4),TextWrapping=TextWrapping.Wrap,ToolTip="括号为当前值减去基线，百分比属性显示百分点差（pp）；括号中的 — 表示基线未知。未设对比基线时仅使用游戏已确认的原生基准，其余显示基线未知；不会用第一次观测值冒充基础值。"};DockPanel.SetDock(legend,Dock.Top);root.Children.Add(legend);
        DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
        var scroll=new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroll);
        scroll.SizeChanged+=(_,_)=>LayoutGroups(scroll.ActualWidth);
        foreach(var (left,top) in new[]{(true,true),(false,true),(true,false),(false,false)}){
            var grip=new Thumb{Width=16,Height=16,Cursor=left==top?Cursors.SizeNWSE:Cursors.SizeNESW,HorizontalAlignment=left?HorizontalAlignment.Left:HorizontalAlignment.Right,VerticalAlignment=top?VerticalAlignment.Top:VerticalAlignment.Bottom,ToolTip="拖动此角调整宽高"};
            var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,Brushes.Transparent);var mark=new FrameworkElementFactory(typeof(TextBlock));mark.SetValue(TextBlock.TextProperty,"◆");mark.SetValue(TextBlock.ForegroundProperty,Brushes.DarkKhaki);mark.SetValue(TextBlock.FontSizeProperty,9.0);border.AppendChild(mark);grip.Template=new ControlTemplate(typeof(Thumb)){VisualTree=border};
            double startX=0,startY=0,startW=0,startH=0;Native.Point mouseStart=default;
            grip.DragStarted+=(_,_)=>{dragging=true;startX=Left;startY=Top;startW=Width;startH=Height;Native.GetCursorPos(out mouseStart);};
            grip.DragDelta+=(_,_)=>{if(!Native.GetCursorPos(out var mouse))return;var dpi=VisualTreeHelper.GetDpi(this);double dx=(mouse.X-mouseStart.X)/dpi.DpiScaleX,dy=(mouse.Y-mouseStart.Y)/dpi.DpiScaleY;Width=Math.Clamp(startW+(left?-dx:dx),MinWidth,1800);Height=Math.Clamp(startH+(top?-dy:dy),MinHeight,1400);Left=left?startX+startW-Width:startX;Top=top?startY+startH-Height:startY;};
            grip.DragCompleted+=(_,e)=>{if(e.Canceled){Left=startX;Top=startY;Width=startW;Height=startH;}dragging=false;owner.Preferences.StatsWidth=Width;owner.Preferences.StatsHeight=Height;owner.Preferences.StatsX=Left;owner.Preferences.StatsY=Top;owner.Preferences.StatsPlaced=true;owner.SavePreferences();};shell.Children.Add(grip);
        }
        SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowLongPtr(h,-20,Native.GetWindowLongPtr(h,-20)|(App.TestWindows?0x08040000:0x08000080));};
        Rebuild();
    }
    public void ApplyAppearance(){surface.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(owner.Preferences.StatsOpacity*255),25,23,31));surface.BorderThickness=new Thickness(owner.Preferences.StatsBorder?1:0);FontSize=owner.Preferences.StatsFontSize;}
    private void Rebuild(){
        rows.Children.Clear();controls.Clear();groups.Clear();
        var visible=StatsCatalog.All.Where(s=>(!owner.Preferences.StatsFavoritesOnly||owner.Preferences.FavoriteStats.Contains(s.Key))).ToArray();
        foreach(var group in visible.GroupBy(s=>s.Group)){
            var groupRows=new StackPanel();groups.Add(groupRows);rows.Children.Add(groupRows);
            groupRows.Children.Add(new TextBlock{Text=group.Key,Foreground=Brushes.DarkKhaki,FontSize=12,Margin=new Thickness(12,12,8,3)});
            foreach(var definition in group){
                var grid=new Grid{Margin=new Thickness(10,2,6,2)};grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});grid.ColumnDefinitions.Add(new(){Width=new GridLength(26)});
                var label=new TextBlock{Text=definition.Name,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,5,0),VerticalAlignment=VerticalAlignment.Center};grid.Children.Add(label);
                var value=new TextBlock{MinWidth=55,TextAlignment=TextAlignment.Right,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(value,1);grid.Children.Add(value);
                bool starred=owner.Preferences.FavoriteStats.Contains(definition.Key);var star=new Button{Content=starred?"★":"☆",Padding=new Thickness(0),Margin=new Thickness(0),Height=22,Foreground=Brushes.Goldenrod,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Focusable=false,Opacity=starred?1:0,ToolTip="加入／移出特别关注"};Grid.SetColumn(star,2);grid.Children.Add(star);
                var row=new Border{Child=grid,Background=Brushes.Transparent};groupRows.Children.Add(row);controls[definition.Key]=(value,row,star);
                row.MouseEnter+=(_,_)=>{star.Opacity=1;row.Background=new SolidColorBrush(Color.FromArgb(35,255,255,255));};row.MouseLeave+=(_,_)=>{star.Opacity=owner.Preferences.FavoriteStats.Contains(definition.Key)?1:0;row.Background=Brushes.Transparent;};
                var tip=new ToolTip{MaxWidth=440};row.ToolTip=tip;ToolTipService.SetInitialShowDelay(row,180);ToolTipService.SetShowDuration(row,60000);
                row.ToolTipOpening+=(_,_)=>tip.Content=BuildExplanation(definition);
                star.Click+=(_,_)=>{if(!owner.Preferences.FavoriteStats.Add(definition.Key))owner.Preferences.FavoriteStats.Remove(definition.Key);owner.SavePreferences();Rebuild();};
            }
        }
        if(visible.Length==0)rows.Children.Add(new TextBlock{Text="还没有关注的属性。\n切换到“全部”，移到属性上点击 ☆。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(12)});
        LayoutGroups(ActualWidth-20);Refresh(snapshot);
    }
    private void LayoutGroups(double width){double available=Math.Max(225,width-14);int columns=Math.Max(1,(int)(available/330));rows.Width=available;foreach(var group in groups)group.Width=Math.Floor(available/columns);}
    private double? Reference(StatReading? stat)=>stat is null?null:baseline.TryGetValue(stat.Key,out double captured)?captured:stat.Baseline;
    private static readonly Brush Positive=new SolidColorBrush(Color.FromRgb(112,220,155));
    private static readonly Brush Negative=new SolidColorBrush(Color.FromRgb(255,132,135));
    private static Brush DeltaBrush(string key,double? delta)=>StatPresentation.Benefit(key,delta) switch {1=>Positive,-1=>Negative,_=>Brushes.Silver};
    internal FrameworkElement BuildExplanation(StatDefinition definition){
        var panel=new StackPanel{Width=370};
        void Text(string text,Brush? color=null,double size=12)=>panel.Children.Add(new TextBlock{Text=text,Foreground=color??Brushes.Silver,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,3)});
        void Heading(string text){panel.Children.Add(new Border{Height=1,Background=new SolidColorBrush(Color.FromRgb(75,63,86)),Margin=new Thickness(0,9,0,6)});Text(text,Brushes.Wheat,13);}
        Text("◆ "+definition.Name,Brushes.Wheat,17);
        if(!snapshot.Fresh(Environment.TickCount64)){Text("◷ 数据已过期，等待游戏更新。");return panel;}
        var stat=snapshot.Stats.FirstOrDefault(s=>s.Key==definition.Key);double? reference=Reference(stat),delta=stat?.Value-reference;
        Text("当前  "+CharacterTelemetry.Format(stat?.Value,definition.Unit),Brushes.White,22);
        Text(StatPresentation.Meaning(definition.Key,delta)+"  "+(delta>0?"▲ ":delta<0?"▼ ":"")+StatPresentation.Delta(delta,definition.Unit),DeltaBrush(definition.Key,delta),16);
        Text(StatPresentation.Direction(definition.Key) switch {1=>"数值越高通常越好",-1=>"数值越低通常越好（下降也会显示绿色）",_=>"此项方向尚未确认，暂不判定好坏"});
        Heading("◎ 对比基线");
        Text("基线  "+CharacterTelemetry.Format(reference,definition.Unit),Brushes.Wheat);
        Text(reference is null?"尚无可信基线，可点击“设为基线”记录。":baseline.ContainsKey(definition.Key)?$"{baselineAt} 手动记录 · 本次角色会话":"游戏已确认的原生基准");
        if(baseline.ContainsKey(definition.Key))Text("原生基础值  "+CharacterTelemetry.Format(stat?.Baseline,definition.Unit));
        Text("净变化 = 当前 − 对比基线；pp 表示百分点。");
        var contributions=StatMechanics.Contributions(snapshot,definition.Key).ToArray();
        if(contributions.Length>0){Heading("◇ 基础属性影响");foreach(var entry in contributions)Text((entry.Delta>=0?"▲ ":"▼ ")+entry.Label+"  →  "+StatPresentation.Delta(entry.Delta,definition.Unit),DeltaBrush(definition.Key,entry.Delta));}
        Heading("✦ 游戏记录的来源 · 原始修正");
        var sources=snapshot.Sources.Where(s=>s.Key==definition.Key).ToArray();
        foreach(var source in sources){string label=string.IsNullOrWhiteSpace(source.Label)?$"来源 #{source.SourceId:0}":StatMechanics.CleanLabel(source.Label);Text((source.Delta>0?"▲ ":source.Delta<0?"▼ ":"◇ ")+label+"  "+StatPresentation.Delta(source.Delta,""),DeltaBrush(definition.Key,source.Delta));}
        if(sources.Length==0)Text(snapshot.SourcesAvailable?"没有此项的来源记录。":"当前无法取得来源明细。");
        Text("来源记录不等于与所选基线相比的新增效果；可能包含装备、技能与状态，不能与基础属性影响重复相加。乘算、上限和取整不作猜测。");
        return new ScrollViewer{Content=panel,MaxHeight=460,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    }
    public void Refresh(CharacterTelemetry data){
        snapshot=data;bool fresh=data.Fresh(Environment.TickCount64);
        if(!fresh||baselinePlayer!=data.Player){baseline.Clear();baselinePlayer=data.Player;baselineAt="";}
        status.Text=(owner.Preferences.StatsFavoritesOnly?"★ 特别关注":"全部属性")+" · "+(fresh?(baseline.Count>0?$"基线 {baselineAt}":"实时 · 基线未知项可手动记录"):"等待新鲜数据，不显示旧数值");
        foreach(var (key,control) in controls){var definition=StatsCatalog.All.First(d=>d.Key==key);var stat=fresh?data.Stats.FirstOrDefault(s=>s.Key==key):null;double? reference=Reference(stat),delta=stat?.Value-reference;control.Value.Inlines.Clear();control.Value.Inlines.Add(new Run(CharacterTelemetry.Format(stat?.Value,definition.Unit)){Foreground=Brushes.White});if(stat?.Value is not null)control.Value.Inlines.Add(new Run("（"+StatPresentation.Delta(delta,definition.Unit)+"）"){Foreground=DeltaBrush(key,delta),FontWeight=FontWeights.SemiBold});}
    }
    public void Place(Native.Rect rect){
        if(dragging)return;
        var dpi=VisualTreeHelper.GetDpi(this);double left=rect.Left/dpi.DpiScaleX,top=rect.Top/dpi.DpiScaleY,w=(rect.Right-rect.Left)/dpi.DpiScaleX,h=(rect.Bottom-rect.Top)/dpi.DpiScaleY;
        Height=Math.Max(200,Math.Min(owner.Preferences.StatsHeight,h-32));Width=Math.Max(250,Math.Min(owner.Preferences.StatsWidth,w-24));
        Left=Math.Clamp(owner.Preferences.StatsPlaced?owner.Preferences.StatsX:left+12,left,Math.Max(left,left+w-Width));Top=Math.Clamp(owner.Preferences.StatsPlaced?owner.Preferences.StatsY:top+Math.Max(12,(h-Height)/2),top,Math.Max(top,top+h-Height));
    }
}
