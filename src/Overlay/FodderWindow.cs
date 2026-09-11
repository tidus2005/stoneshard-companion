using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace StoneshardCompanion;
public sealed class FodderWindow:Window
{
    private readonly MainWindow owner;private readonly StackPanel materials=new();private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,8),Foreground=Brushes.Silver};
    public FodderWindow(MainWindow parent){
        owner=parent;Title="马车饲料 · 材料选择";Width=450;Height=580;MinWidth=360;MinHeight=300;Topmost=true;Background=new SolidColorBrush(Color.FromRgb(25,23,31));Foreground=Brushes.Wheat;
        var root=new DockPanel{Margin=new Thickness(18)};Content=root;
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        header.Children.Add(new TextBlock{Text="沿途采集与饲料制作",FontSize=19,TextWrapping=TextWrapping.Wrap});
        header.Children.Add(new TextBlock{Text="下方勾选仅用于手动一键制作，自动采集无需配置材料。手动制作会消耗所选种类的全部可用材料。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)});
        var automatic=new CheckBox{Content="行走途中自动采集并制作饲料",IsChecked=owner.Preferences.AutoForage,Foreground=Brushes.Wheat,Margin=new Thickness(0,6,0,6)};
        automatic.Click+=(_,_)=>{owner.Preferences.AutoForage=automatic.IsChecked==true;owner.SavePreferences();};header.Children.Add(automatic);
        header.Children.Add(new TextBlock{Text="无需刷新或勾选：行走时自动采集视野内可做饲料的浆果、普通药草（保留大黄、小扁豆和蔬菜等食材），将背包中这类材料制作并合堆后继续原路线。遇敌、手动操作或背包放不下时停止。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver});
        var refresh=new Button{Content="刷新背包材料",Margin=new Thickness(0,3,0,3)};refresh.Click+=(_,_)=>Refresh();header.Children.Add(refresh);
        var clear=new Button{Content="清空选择",Margin=new Thickness(0,3,0,3)};clear.Click+=(_,_)=>{owner.Preferences.FodderMaterials.Clear();owner.SavePreferences();Refresh();};header.Children.Add(clear);
        var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);bottom.Children.Add(status);
        var craft=new Button{Content="一键制作所选材料",Padding=new Thickness(8)};craft.Click+=async(_,_)=>{craft.IsEnabled=false;try{status.Text=await owner.CraftFodder();}finally{craft.IsEnabled=true;}};bottom.Children.Add(craft);
        root.Children.Add(new ScrollViewer{Content=materials,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Refresh();
    }
    private void Refresh(){
        materials.Children.Clear();var data=owner.CharacterData;
        bool fresh=data.Fresh(Environment.TickCount64)&&data.FoodsComplete;
        var available=(fresh?data.Foods:[]).GroupBy(f=>f.Key).ToDictionary(g=>g.Key,g=>g.ToArray());
        bool changed=false;
        foreach(var (key,items) in available){string name=items.First().Name;if(!string.IsNullOrWhiteSpace(name)&&owner.Preferences.KnownFodderMaterials.GetValueOrDefault(key)!=name){owner.Preferences.KnownFodderMaterials[key]=name;changed=true;}}
        if(changed)owner.SavePreferences();
        var known=owner.Preferences.KnownFodderMaterials.Keys.Union(owner.Preferences.FodderMaterials).OrderBy(k=>owner.Preferences.KnownFodderMaterials.GetValueOrDefault(k,k)).ToArray();
        foreach(var key in known){
            string name=owner.Preferences.KnownFodderMaterials.GetValueOrDefault(key,key.Replace("o_inv_",""));
            var items=available.GetValueOrDefault(key,[]);
            var check=new CheckBox{Content=$"{name} · {(fresh?items.Length+" 组":"待连接")}  · 饲料值 {items.Sum(f=>f.Value):0.##}",IsChecked=owner.Preferences.FodderMaterials.Contains(key),Foreground=Brushes.Wheat,Margin=new Thickness(4,8,4,8)};
            check.Click+=(_,_)=>{if(check.IsChecked==true)owner.Preferences.FodderMaterials.Add(key);else owner.Preferences.FodderMaterials.Remove(key);owner.SavePreferences();};materials.Children.Add(check);
        }
        status.Text=!fresh?"等待完整的新鲜背包数据；已记住的材料仍可配置。":known.Length==0?"背包内没有原版可转为饲料的材料。捡到后点击刷新。":"材料用完后也会保留勾选；下次拾取继续按该配置制作。";
    }
}
