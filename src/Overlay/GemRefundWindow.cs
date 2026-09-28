using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace StoneshardCompanion;

public sealed class GemRefundWindow:Window
{
    private readonly LiveBuildSnapshot snapshot;
    private readonly TextBox[] quantities=new TextBox[LiveBuild.GemKeys.Length];
    private readonly Button[] minus=new Button[LiveBuild.GemKeys.Length],plus=new Button[LiveBuild.GemKeys.Length];
    private readonly TextBlock total=new(){TextWrapping=TextWrapping.Wrap,FontSize=17,Foreground=Brushes.Wheat};
    private readonly TextBlock detail=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver,Margin=new Thickness(0,5,0,0)};
    private readonly Button use=new(){Content="使用这些宝石",Padding=new Thickness(16,8,16,8),IsEnabled=false};
    public int[]? SelectedMaterials {get;private set;}

    public GemRefundWindow(LiveBuildSnapshot snapshot,string name){
        this.snapshot=snapshot;int available=LiveBuild.MaterialValue(snapshot.Gems);
        Title="选择退点宝石";Width=640;Height=900;MinWidth=580;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(25,25,29));Foreground=Brushes.Gainsboro;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=14;
        var root=new DockPanel{Margin=new Thickness(22)};Content=root;
        var header=new StackPanel{Margin=new Thickness(0,0,0,14)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        header.Children.Add(new TextBlock{Text=$"退回「{name}」1 点",FontSize=23,Foreground=Brushes.Wheat});
        header.Children.Add(new TextBlock{Text=$"任意宝石可混搭，基础价值合计 ≥ {LiveBuild.RefundValue} 即可。\n钻石 1 颗 = 600；按固定基础价值计算，不受商人收购价影响。\n每次另消耗 1 次历练机会，不扣金币；超出价值不找零。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10),Foreground=Brushes.Silver});
        header.Children.Add(new TextBlock{Text=$"I 主背包可用价值 {available}  ·  历练 {snapshot.Credits} / 6",Margin=new Thickness(0,0,0,10)});
        var auto=new Button{Content="自动凑够 600",Padding=new Thickness(12,6,12,6),HorizontalAlignment=HorizontalAlignment.Left,IsEnabled=available>=LiveBuild.RefundValue,ToolTip="先选总价值最低的达标组合；价值相同时优先少用宝石。"};header.Children.Add(auto);
        var footer=new StackPanel{Margin=new Thickness(0,14,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);footer.Children.Add(total);footer.Children.Add(detail);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};footer.Children.Add(actions);
        actions.Children.Add(new Button{Content="取消",IsCancel=true,Padding=new Thickness(16,8,16,8),Margin=new Thickness(0,0,10,0)});actions.Children.Add(use);
        var rows=new StackPanel();root.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var labels=new Grid{Margin=new Thickness(0,0,0,8)};labels.ColumnDefinitions.Add(new ColumnDefinition());labels.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(60)});labels.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(148)});rows.Children.Add(labels);
        foreach(var (text,column) in new[]{("宝石 / 单颗基础价值",0),("持有",1),("本次消耗",2)}){var label=new TextBlock{Text=text,Foreground=Brushes.Silver};Grid.SetColumn(label,column);labels.Children.Add(label);}
        foreach(int g in Enumerable.Range(0,quantities.Length).OrderBy(i=>LiveBuild.GemValues[i])){
            int index=g;var row=new Grid{Margin=new Thickness(0,0,0,6)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(60)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(148)});rows.Children.Add(row);
            row.Children.Add(new TextBlock{Text=$"{LiveBuild.GemNames[g]}  ·  {LiveBuild.GemValues[g]}",VerticalAlignment=VerticalAlignment.Center});
            var owned=new TextBlock{Text=snapshot.Gems[g].ToString(),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(owned,1);row.Children.Add(owned);
            var stepper=new StackPanel{Orientation=Orientation.Horizontal};Grid.SetColumn(stepper,2);row.Children.Add(stepper);
            minus[g]=new Button{Content="−",Width=32,Height=30};quantities[g]=new TextBox{Text="0",Width=52,Height=30,TextAlignment=TextAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(5,0,5,0)};plus[g]=new Button{Content="+",Width=32,Height=30};
            AutomationProperties.SetName(quantities[g],LiveBuild.GemNames[g]+"消耗数量");AutomationProperties.SetName(minus[g],"减少"+LiveBuild.GemNames[g]);AutomationProperties.SetName(plus[g],"增加"+LiveBuild.GemNames[g]);
            stepper.Children.Add(minus[g]);stepper.Children.Add(quantities[g]);stepper.Children.Add(plus[g]);
            minus[g].Click+=(_,_)=>Step(index,-1);plus[g].Click+=(_,_)=>Step(index,1);quantities[g].TextChanged+=(_,_)=>RefreshSelection();
        }
        auto.Click+=(_,_)=>{var plan=LiveBuild.SuggestMaterials(snapshot.Gems);if(plan is not null)for(int i=0;i<plan.Length;i++)quantities[i].Text=plan[i].ToString(CultureInfo.InvariantCulture);};
        use.Click+=(_,_)=>{var selected=ReadSelection();if(selected is null||snapshot.Credits<1||LiveBuild.MaterialValue(selected)<LiveBuild.RefundValue)return;SelectedMaterials=selected;DialogResult=true;};
        RefreshSelection();
    }
    private int[]? ReadSelection(){
        var selected=new int[quantities.Length];
        for(int i=0;i<selected.Length;i++)if(quantities[i] is null||!int.TryParse(quantities[i].Text,NumberStyles.None,CultureInfo.InvariantCulture,out selected[i])||selected[i]<0||selected[i]>snapshot.Gems[i])return null;
        return selected;
    }
    private void Step(int index,int delta){if(!int.TryParse(quantities[index].Text,out int n))n=0;quantities[index].Text=Math.Clamp(n+delta,0,snapshot.Gems[index]).ToString(CultureInfo.InvariantCulture);}
    private void RefreshSelection(){
        if(quantities.Any(q=>q is null))return;
        var selected=ReadSelection();use.IsEnabled=false;
        for(int i=0;i<quantities.Length;i++){bool valid=int.TryParse(quantities[i].Text,NumberStyles.None,CultureInfo.InvariantCulture,out int n)&&n>=0&&n<=snapshot.Gems[i];minus[i].IsEnabled=valid&&n>0;plus[i].IsEnabled=valid&&n<snapshot.Gems[i];}
        if(selected is null){total.Text="请填写不超过持有量的非负整数";detail.Text="数量有误，无法继续；只会消耗最终确认的宝石。";return;}
        int value=LiveBuild.MaterialValue(selected);total.Text=$"已选价值 {value} / {LiveBuild.RefundValue}";
        detail.Text=value<LiveBuild.RefundValue?$"还差 {LiveBuild.RefundValue-value}。可手动混搭，或点击自动凑够。":value==LiveBuild.RefundValue?"刚好达标。将消耗所选宝石和 1 次历练机会。":$"已达标，超出 {value-LiveBuild.RefundValue}；所选宝石全部消耗，不找零。";
        if(snapshot.Credits<1)detail.Text+=" 历练机会不足，请先交付契约。";
        use.IsEnabled=value>=LiveBuild.RefundValue&&snapshot.Credits>0;
    }
}
