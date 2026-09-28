using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace StoneshardCompanion;

// An explicitly opened, scrollable snapshot supplements the click-through card.
public sealed class CombatInspectorWindow : Window
{
    public CombatInspectorWindow(CombatTelemetry snapshot,CharacterTelemetry player){
        Title="目标完整数据 · "+StatMechanics.CleanLabel(snapshot.Name);Width=740;Height=760;MinWidth=580;MinHeight=420;
        Background=new SolidColorBrush(Color.FromRgb(22,25,31));Foreground=Brushes.Gainsboro;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
        var root=new DockPanel{Margin=new Thickness(18)};Content=root;
        var header=new TextBlock{Text=$"{StatMechanics.CleanLabel(snapshot.Name)}\n{DateTime.Now:HH:mm:ss} 的只读快照 · 不随目标移动更新 · Esc 关闭",FontSize=16,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var rows=new StackPanel();root.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        void Text(string text,Brush? brush=null){rows.Children.Add(new TextBlock{Text=text,Foreground=brush??Brushes.Gainsboro,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,4)});}
        Text("属性                                   我                  目标",Brushes.Goldenrod);
        foreach(string key in snapshot.Player.Keys.Concat(snapshot.Target.Keys).Distinct().OrderBy(k=>StatsCatalog.All.FirstOrDefault(d=>d.Key==k)?.Group).ThenBy(k=>k)){
            var d=StatsCatalog.All.FirstOrDefault(s=>s.Key==key);var line=new Grid{Margin=new Thickness(0,2,0,2)};
            foreach(int weight in new[]{2,1,1})line.ColumnDefinitions.Add(new(){Width=new GridLength(weight,GridUnitType.Star)});
            string label=key switch{"DEF"=>"护甲防护","ArmorDurability"=>"护甲耐久（原始值）","Block_PowerMax"=>"格挡能量上限","is_range"=>"远程类型标记","is_mage"=>"施法类型标记","Hit_Chance"=>"准确率","Stun_Resistance"=>"控制抗性",_=>d?.Name??key};
            string[] values=[label,CombatPreview.Format(CombatPreview.Value(snapshot.Player,key),d?.Unit??""),CombatPreview.Format(CombatPreview.Value(snapshot.Target,key),d?.Unit??"")];
            for(int i=0;i<3;i++){var value=new TextBlock{Text=values[i],TextWrapping=TextWrapping.Wrap};Grid.SetColumn(value,i);line.Children.Add(value);}rows.Children.Add(line);
        }
        Text("目标状态与伤势",Brushes.Goldenrod);
        foreach(var buff in snapshot.Buffs)Text("• "+StatMechanics.CleanLabel(buff.Name));
        if(snapshot.Buffs.Length==0)Text(snapshot.BuffsComplete?"未见活动状态。":"状态列表未读取。");
        if(!snapshot.BuffsComplete)Text("列表不完整；未读取到的状态不能视为不存在。");
        Text("我的属性修正来源",Brushes.Goldenrod);
        if(player.SourcesAvailable&&player.Fresh(snapshot.At))foreach(var source in player.Sources){var d=StatsCatalog.All.FirstOrDefault(s=>s.Key==source.Key);Text($"{StatMechanics.CleanLabel(source.Label)} · {d?.Name??source.Key}：{CombatPreview.Format(source.Delta)}");}
        else Text("当前来源明细不可用。");
        Text("当前属性已包含生效状态。上面列出的是可读取字段，不包含敌人技能清单；部位伤势、各部位护甲、技能条件加成及未来回合行为尚未完整适配。",Brushes.DarkGray);
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape)Close();};
    }
}
