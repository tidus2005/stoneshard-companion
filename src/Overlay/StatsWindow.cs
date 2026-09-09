using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace StoneshardCompanion;
public sealed class StatsWindow:Window
{
    private readonly MainWindow owner;
    private readonly StackPanel rows=new();
    private readonly TextBlock status=new(){Foreground=Brushes.Silver,FontSize=11,Margin=new Thickness(10)};
    private readonly Dictionary<string,(TextBlock Value,Border Row,Button Star)> controls=[];
    private CharacterTelemetry snapshot=new();
    private bool dragging;
    private string category="全部分类";
    private readonly ComboBox categories=new(){MinWidth=145,Margin=new Thickness(4)};
    public StatsWindow(MainWindow parent){
        owner=parent;Title="晶石助手 · 角色属性";Width=330;Height=600;MinWidth=270;MinHeight=240;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;
        Topmost=true;ShowActivated=false;ShowInTaskbar=false;FontFamily=new FontFamily("Microsoft YaHei UI");Foreground=Brushes.Wheat;
        var root=new DockPanel();Content=new Border{Background=new SolidColorBrush(Color.FromArgb(210,25,23,31)),BorderBrush=new SolidColorBrush(Color.FromRgb(112,98,71)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=root};
        var header=new DockPanel{Margin=new Thickness(10)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var close=new Button{Content="隐藏",Focusable=false};DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);close.Click+=(_,_)=>{owner.Preferences.ShowStats=false;owner.SavePreferences();Hide();};
        var title=new TextBlock{Text="角色属性",FontSize=17};header.Children.Add(title);
        title.MouseLeftButtonDown+=(_,e)=>{if(e.ClickCount==1){try{dragging=true;DragMove();owner.Preferences.StatsX=Left;owner.Preferences.StatsY=Top;owner.Preferences.StatsPlaced=true;owner.SavePreferences();}catch(InvalidOperationException){}finally{dragging=false;}}};
        var tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(6,0,6,4)};DockPanel.SetDock(tabs,Dock.Top);root.Children.Add(tabs);
        var all=new Button{Content="全部",Margin=new Thickness(3),Focusable=false};var favorites=new Button{Content="★ 特别关注",Margin=new Thickness(3),Focusable=false};tabs.Children.Add(all);tabs.Children.Add(favorites);
        all.Click+=(_,_)=>{owner.Preferences.StatsFavoritesOnly=false;owner.SavePreferences();Rebuild();};favorites.Click+=(_,_)=>{owner.Preferences.StatsFavoritesOnly=true;owner.SavePreferences();Rebuild();};
        categories.Items.Add("全部分类");foreach(var group in StatsCatalog.All.Select(x=>x.Group).Distinct())categories.Items.Add(group);categories.SelectedIndex=0;
        categories.SelectionChanged+=(_,_)=>{category=categories.SelectedItem?.ToString()??"全部分类";Rebuild();};
        DockPanel.SetDock(categories,Dock.Top);root.Children.Add(categories);DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
        root.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowLongPtr(h,-20,Native.GetWindowLongPtr(h,-20)|0x08000080);};
        Rebuild();
    }
    private void Rebuild(){
        rows.Children.Clear();controls.Clear();
        var visible=StatsCatalog.All.Where(s=>(category=="全部分类"||s.Group==category)&&(!owner.Preferences.StatsFavoritesOnly||owner.Preferences.FavoriteStats.Contains(s.Key))).ToArray();
        foreach(var group in visible.GroupBy(s=>s.Group)){
            rows.Children.Add(new TextBlock{Text=group.Key,Foreground=Brushes.DarkKhaki,FontSize=12,Margin=new Thickness(12,12,8,3)});
            foreach(var definition in group){
                var grid=new Grid{Margin=new Thickness(10,5,6,5)};grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});grid.ColumnDefinitions.Add(new(){Width=new GridLength(30)});
                var label=new TextBlock{Text=definition.Name,VerticalAlignment=VerticalAlignment.Center};grid.Children.Add(label);
                var value=new TextBlock{MinWidth=55,TextAlignment=TextAlignment.Right,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(value,1);grid.Children.Add(value);
                bool starred=owner.Preferences.FavoriteStats.Contains(definition.Key);var star=new Button{Content=starred?"★":"☆",Foreground=Brushes.Goldenrod,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Focusable=false,Opacity=starred?1:0,ToolTip="加入／移出特别关注"};Grid.SetColumn(star,2);grid.Children.Add(star);
                var row=new Border{Child=grid,Background=Brushes.Transparent};rows.Children.Add(row);controls[definition.Key]=(value,row,star);
                row.MouseEnter+=(_,_)=>{star.Opacity=1;row.Background=new SolidColorBrush(Color.FromArgb(35,255,255,255));};row.MouseLeave+=(_,_)=>{star.Opacity=owner.Preferences.FavoriteStats.Contains(definition.Key)?1:0;row.Background=Brushes.Transparent;};
                row.ToolTipOpening+=(_,_)=>row.ToolTip=snapshot.Fresh(Environment.TickCount64)?snapshot.Explain(definition):"数据已过期，等待游戏更新。";row.ToolTip="属性来源";
                star.Click+=(_,_)=>{if(!owner.Preferences.FavoriteStats.Add(definition.Key))owner.Preferences.FavoriteStats.Remove(definition.Key);owner.SavePreferences();Rebuild();};
            }
        }
        if(visible.Length==0)rows.Children.Add(new TextBlock{Text="还没有关注的属性。\n切换到“全部”，移到属性上点击 ☆。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(12)});
        Refresh(snapshot);
    }
    public void Refresh(CharacterTelemetry data){snapshot=data;bool fresh=data.Fresh(Environment.TickCount64);status.Text=(owner.Preferences.StatsFavoritesOnly?"★ 特别关注":"全部属性")+" · "+(fresh?"实时读取 · 悬停查看来源":"等待新鲜数据，不显示旧数值");
        foreach(var (key,control) in controls){var definition=StatsCatalog.All.First(d=>d.Key==key);var stat=fresh?data.Stats.FirstOrDefault(s=>s.Key==key):null;control.Value.Text=CharacterTelemetry.Format(stat?.Value,definition.Unit);}
    }
    public void Place(Native.Rect rect){
        if(dragging)return;
        var dpi=VisualTreeHelper.GetDpi(this);double left=rect.Left/dpi.DpiScaleX,top=rect.Top/dpi.DpiScaleY,w=(rect.Right-rect.Left)/dpi.DpiScaleX,h=(rect.Bottom-rect.Top)/dpi.DpiScaleY;
        Height=Math.Max(240,Math.Min(600,h-32));Width=Math.Max(270,Math.Min(330,w-24));
        Left=Math.Clamp(owner.Preferences.StatsPlaced?owner.Preferences.StatsX:left+12,left,Math.Max(left,left+w-Width));Top=Math.Clamp(owner.Preferences.StatsPlaced?owner.Preferences.StatsY:top+Math.Max(12,(h-Height)/2),top,Math.Max(top,top+h-Height));
    }
}
