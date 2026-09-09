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
        header.Children.Add(new TextBlock{Text="把选中的背包材料制成饲料",FontSize=19,TextWrapping=TextWrapping.Wrap});
        header.Children.Add(new TextBlock{Text="只列出原版可用、位于主背包的材料。制作会消耗所选种类的全部可用材料；不自动拾取地面物品。没有勾选的材料会保留。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)});
        var refresh=new Button{Content="刷新背包材料",Margin=new Thickness(0,3,0,3)};refresh.Click+=(_,_)=>Refresh();header.Children.Add(refresh);
        var clear=new Button{Content="清空选择",Margin=new Thickness(0,3,0,3)};clear.Click+=(_,_)=>{owner.Preferences.FodderMaterials.Clear();owner.SavePreferences();Refresh();};header.Children.Add(clear);
        var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);bottom.Children.Add(status);
        var craft=new Button{Content="一键制作所选材料",Padding=new Thickness(8)};craft.Click+=async(_,_)=>{craft.IsEnabled=false;try{status.Text=await owner.CraftFodder();}finally{craft.IsEnabled=true;}};bottom.Children.Add(craft);
        root.Children.Add(new ScrollViewer{Content=materials,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Refresh();
    }
    private void Refresh(){
        materials.Children.Clear();var data=owner.CharacterData;
        if(!data.Fresh(Environment.TickCount64)||!data.FoodsComplete){status.Text="等待游戏连接和完整的新鲜背包数据；清单不完整时不会制作。";return;}
        var available=data.Foods.GroupBy(f=>f.Key).OrderBy(g=>g.First().Name).ToArray();
        foreach(var group in available){
            string name=group.Select(f=>f.Name).FirstOrDefault(n=>!string.IsNullOrWhiteSpace(n))??group.Key.Replace("o_inv_","");
            var check=new CheckBox{Content=$"{name} · {group.Count()} 组  · 饲料值 {group.Sum(f=>f.Value):0.##}",IsChecked=owner.Preferences.FodderMaterials.Contains(group.Key),Foreground=Brushes.Wheat,Margin=new Thickness(4,8,4,8)};
            check.Click+=(_,_)=>{if(check.IsChecked==true)owner.Preferences.FodderMaterials.Add(group.Key);else owner.Preferences.FodderMaterials.Remove(group.Key);owner.SavePreferences();};materials.Children.Add(check);
        }
        status.Text=available.Length==0?"背包内没有原版可转为饲料的材料。":"勾选会保存。只在游戏前台、安全、空闲且原版面板关闭时执行一次。";
    }
}
