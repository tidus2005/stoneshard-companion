using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
namespace StoneshardCompanion;
public sealed class FodderWindow:Window
{
    private readonly MainWindow owner;
    private readonly StackPanel rows=new();
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver,Margin=new Thickness(0,8,0,0)};
    private readonly Dictionary<string,TextBlock> counts=[];
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    public FodderWindow(MainWindow parent){
        owner=parent;Title="沿途采集 · 保留数量与饲料";Width=790;Height=720;MinWidth=670;MinHeight=400;Topmost=true;
        Background=new SolidColorBrush(Color.FromRgb(25,23,31));Foreground=Brushes.Wheat;FontFamily=new FontFamily("Microsoft YaHei UI");
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/StoneshardCompanion;component/PanelTheme.xaml",UriKind.Relative)});
        var root=new DockPanel{Margin=new Thickness(18)};Content=root;
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        header.Children.Add(new TextBlock{Text="沿途采集配置",FontSize=21});
        var automatic=new CheckBox{Content="行走途中按下表自动采集",IsChecked=owner.Preferences.AutoForage,Margin=new Thickness(0,12,0,10)};
        automatic.Click+=(_,_)=>{owner.Preferences.AutoForage=automatic.IsChecked==true;owner.SavePreferences();};header.Children.Add(automatic);
        header.Children.Add(new TextBlock{Text="勾选物品后，数量不足时沿途补齐；启用“超量转饲料”则继续采集，只转换超出保留量的部分。蘑菇等无原版饲料产值的物品，到量即停。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver,Margin=new Thickness(0,0,0,8)});
        header.Children.Add(new TextBlock{Text="数量按件／原版堆叠计，不按食用次数；不拆堆，整堆转换会越过保留线时保留整堆。小扁豆植株可勾选自动剥取：开启后把植株剥成豆籽，此行最低保留按豆籽计，与豆籽行取较高值。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkKhaki,FontSize=11,Margin=new Thickness(0,0,0,10)});
        header.Children.Add(MakeRow(new TextBlock{Text="采集物品"},new TextBlock{Text="背包"},new TextBlock{Text="最低保留"},new TextBlock{Text="超量转饲料"},new TextBlock{Text="自动剥取"}));
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);footer.Children.Add(status);
        footer.Children.Add(new TextBlock{Text="开启自动采集后，打开背包并停止操作片刻，会自动丢弃腐烂浆果，并剥取已勾选的小扁豆植株。仅处理背包物品；原版拒绝时停止，关闭再打开背包可重试。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver,FontSize=11,Margin=new Thickness(0,8,0,0)});
        root.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        rows.Children.Add(new TextBlock{Text="食用蘑菇共享组包含：松乳菇、牛肝菌、鸡油菌、羊肚菌。开启后四种合计达到保留量即停止；覆盖四种单独规则，不包括毒伞菇和毒蝇伞。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkKhaki});
        foreach(var group in ForagePolicy.All.OrderByDescending(d=>d.Key=="@edible_mushrooms").GroupBy(d=>d.Group)){
            rows.Children.Add(new TextBlock{Text=group.Key,Foreground=Brushes.DarkKhaki,Margin=new Thickness(0,16,0,7),FontSize=15});
            foreach(var d in group){
                var rule=owner.Preferences.ForageRules[d.Key];
                var selected=new CheckBox{Content=d.Name,IsChecked=rule.Enabled,ToolTip=d.Key};selected.Click+=(_,_)=>{rule.Enabled=selected.IsChecked==true;owner.SavePreferences();};
                var count=new TextBlock{Text="—",VerticalAlignment=VerticalAlignment.Center};counts[d.Key]=count;
                var keep=new TextBox{Text=rule.Keep.ToString(),MaxLength=3,Width=42,HorizontalAlignment=HorizontalAlignment.Left,ToolTip="0–999 件，失去焦点或回车后保存"};
                void SaveKeep(){if(int.TryParse(keep.Text,out int n)&&n is >=0 and <=999){rule.Keep=n;owner.SavePreferences();status.Text="配置已保存。";}else{keep.Text=rule.Keep.ToString();status.Text="保留数量需为 0–999，已恢复上次有效值。";}}
                keep.LostKeyboardFocus+=(_,_)=>SaveKeep();keep.KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter){SaveKeep();e.Handled=true;}};
                var quantity=new StackPanel{Orientation=Orientation.Horizontal};quantity.Children.Add(keep);
                var steps=new StackPanel{Margin=new Thickness(2,0,0,0)};quantity.Children.Add(steps);
                foreach(var (symbol,delta) in new[]{("▲",1),("▼",-1)}){
                    var step=new RepeatButton{Content=symbol,Width=22,Height=15,FontSize=8,Padding=new Thickness(0),Delay=350,Interval=90,ToolTip=delta>0?"增加 1（按住连续增加）":"减少 1（按住连续减少）"};
                    step.Click+=(_,_)=>{int value=int.TryParse(keep.Text,out int typed)?typed:rule.Keep;keep.Text=Math.Clamp((long)value+delta,0,999).ToString();SaveKeep();};steps.Children.Add(step);
                }
                var excess=new CheckBox{IsChecked=rule.SurplusFodder,IsEnabled=d.Fodder,ToolTip=d.Fodder?"只转换超出最低保留量的整件／整堆物品":"原版不能将此物品做成饲料"};excess.Click+=(_,_)=>{rule.SurplusFodder=excess.IsChecked==true;owner.SavePreferences();};
                var peel=new CheckBox{IsChecked=rule.AutoPeel,IsEnabled=d.Peel,ToolTip=d.Peel?"打开背包后把植株自动剥成豆籽省空间；最低保留按剥出的豆籽计，不再留未剥植株":"此物品没有已适配的原版剥取操作"};peel.Click+=(_,_)=>{rule.AutoPeel=peel.IsChecked==true;owner.SavePreferences();};
                rows.Children.Add(new Border{Child=MakeRow(selected,count,quantity,excess,peel),BorderBrush=new SolidColorBrush(Color.FromRgb(49,43,57)),BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(0,7,0,7)});
            }
        }
        var manualRows=new StackPanel();var manual=new Expander{Header="手动制作（独立选择，会消耗所选材料的全部可用数量）",Content=manualRows,Foreground=Brushes.Wheat,Margin=new Thickness(0,20,0,10)};rows.Children.Add(manual);
        void FillManual(){
            manualRows.Children.Clear();var data=owner.CharacterData;bool fresh=data.Fresh(Environment.TickCount64)&&data.FoodsComplete;
            if(fresh)foreach(var food in data.Foods.Where(f=>f.Value>0))owner.Preferences.KnownFodderMaterials[food.Key]=food.Name;
            foreach(var key in owner.Preferences.KnownFodderMaterials.Keys.Union(owner.Preferences.FodderMaterials).Order()){
                var check=new CheckBox{Content=owner.Preferences.KnownFodderMaterials.GetValueOrDefault(key,key),IsChecked=owner.Preferences.FodderMaterials.Contains(key),Margin=new Thickness(4,6,4,6)};
                check.Click+=(_,_)=>{if(check.IsChecked==true)owner.Preferences.FodderMaterials.Add(key);else owner.Preferences.FodderMaterials.Remove(key);owner.SavePreferences();};manualRows.Children.Add(check);
            }
            var refresh=new Button{Content="刷新可制作材料",Margin=new Thickness(0,8,0,3)};refresh.Click+=(_,_)=>FillManual();manualRows.Children.Add(refresh);
            var craft=new Button{Content="手动制作全部所选材料",Margin=new Thickness(0,3,0,3)};craft.Click+=async(_,_)=>{craft.IsEnabled=false;try{status.Text=await owner.CraftFodder();}finally{craft.IsEnabled=true;}};manualRows.Children.Add(craft);
        }
        FillManual();RefreshCounts();timer.Tick+=(_,_)=>{automatic.IsChecked=owner.Preferences.AutoForage;RefreshCounts();};Loaded+=(_,_)=>timer.Start();Closed+=(_,_)=>timer.Stop();
    }
    private static Grid MakeRow(params UIElement[] items){var grid=new Grid();foreach(var width in new[]{double.NaN,55,85,100,80})grid.ColumnDefinitions.Add(new(){Width=double.IsNaN(width)?new GridLength(1,GridUnitType.Star):new GridLength(width)});for(int i=0;i<items.Length;i++){Grid.SetColumn(items[i],i);grid.Children.Add(items[i]);}return grid;}
    private void RefreshCounts(){var data=owner.CharacterData;bool fresh=data.Fresh(Environment.TickCount64)&&data.FoodsComplete;foreach(var (key,label) in counts){var foods=data.Foods.Where(f=>key=="@edible_mushrooms"?f.Key is "o_inv_pinecap" or "o_inv_pennybun" or "o_inv_chanterelle" or "o_inv_morel":f.Key==key).ToArray();label.Text=fresh?foods.Sum(f=>f.Quantity).ToString("0"):"—";if(fresh&&foods.Length>0)label.ToolTip=string.Join("、",foods.Select(f=>f.Name));}if(!fresh)status.Text="背包数据暂不可用；可以先配置，数量不会按零处理。";else if(status.Text.StartsWith("背包数据"))status.Text="背包数量已更新 · 配置自动保存";}
}
