using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;

namespace StoneshardCompanion;
public sealed class BuildEditorWindow:Window
{
    private readonly MainWindow owner;
    private LiveBuildSnapshot? snapshot;
    private GameBuildAssets assets=new();
    private bool working;
    private readonly TextBlock summary=new(){TextWrapping=TextWrapping.Wrap,FontSize=14};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver};
    private readonly StackPanel attributes=new();
    private readonly WrapPanel skills=new();
    private readonly ComboBox category=new(){Width=150,Margin=new Thickness(0,0,12,0)};
    private readonly CheckBox learnedOnly=new(){Content="只看已学",IsChecked=true,Foreground=Brushes.Silver,VerticalAlignment=VerticalAlignment.Center};
    private readonly TextBox search=new(){Width=165,Margin=new Thickness(12,0,0,0),ToolTip="搜索技能名称"};
    private readonly Button refresh=new(){Content="刷新当前角色",Padding=new Thickness(12,7,12,7)};
    private static readonly string[] Categories=["单手剑","单手斧","单手钝器","短刀","双手剑","双手斧","双手钝器","长枪","远程兵器","盾牌","长杖","魔杖","基础动作","双持","战斗","运动","披甲战斗","生存","法术精研","炼金","破坏","火术","地术","电术","秘术","毒术","冰术","星术","灵术"];
    private static string Group(int n)=>n>=0&&n<Categories.Length?Categories[n]:"其他";
    public BuildEditorWindow(MainWindow owner){
        this.owner=owner;Title="逐点退回 · 晶石助手";Width=990;Height=730;MinWidth=840;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=B(25,25,29);Foreground=Brushes.Gainsboro;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
        var root=new DockPanel{Margin=new Thickness(22)};Content=root;
        var head=new StackPanel{Margin=new Thickness(0,0,0,16)};DockPanel.SetDock(head,Dock.Top);root.Children.Add(head);head.Children.Add(new TextBlock{Text="属性与技能 · 逐点退回",FontSize=25,Foreground=Brushes.Wheat});head.Children.Add(summary);head.Children.Add(new TextBlock{Text="点击 − 并确认，退回 1 点。消耗宝石配方 ＋ 1 次历练机会，不扣金币。",Margin=new Thickness(0,8,0,0),Foreground=Brushes.Silver});
        var foot=new StackPanel{Margin=new Thickness(0,14,0,0)};DockPanel.SetDock(foot,Dock.Bottom);root.Children.Add(foot);foot.Children.Add(new ScrollViewer{Content=status,MaxHeight=100,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});refresh.HorizontalAlignment=HorizontalAlignment.Right;refresh.Margin=new Thickness(0,10,0,0);foot.Children.Add(refresh);
        var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(225)});body.ColumnDefinitions.Add(new ColumnDefinition());root.Children.Add(body);var left=new StackPanel{Margin=new Thickness(0,0,18,0)};body.Children.Add(new ScrollViewer{Content=left,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});left.Children.Add(new TextBlock{Text="基础属性",FontSize=18,Margin=new Thickness(0,0,0,12)});left.Children.Add(attributes);left.Children.Add(new TextBlock{Text="无需退出游戏。\n请回到城镇或马车营地，停步、关闭游戏菜单并等待技能冷却。\n\n每交付 1 个契约获得 2 次历练机会，最多存 6 次。首次启用赠送 2 次。\n\n材料请放在 I 主背包中，装备背包或马车仓库中的材料需先取出。\n\n每次退点前保存并备份，成功后再次保存。\n\n退回的点数在游戏原版 C / S 界面重新分配，保留原有学习条件。\n\n等级与经验保留，属性不能低于角色初始值。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver,Margin=new Thickness(0,18,0,0)});
        var right=new DockPanel();Grid.SetColumn(right,1);body.Children.Add(right);var filters=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(filters,Dock.Top);right.Children.Add(filters);filters.Children.Add(category);filters.Children.Add(learnedOnly);filters.Children.Add(search);right.Children.Add(new ScrollViewer{Content=skills,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        category.ItemsSource=new[]{"全部技能"}.Concat(Categories).ToArray();category.SelectedIndex=0;category.SelectionChanged+=(_,_)=>RenderSkills();learnedOnly.Checked+=(_,_)=>RenderSkills();learnedOnly.Unchecked+=(_,_)=>RenderSkills();search.TextChanged+=(_,_)=>RenderSkills();refresh.Click+=async(_,_)=>await Reload();Loaded+=async(_,_)=>await Reload();Closing+=(_,e)=>{if(working)e.Cancel=true;};
    }
    private static SolidColorBrush B(byte r,byte g,byte b)=>new(Color.FromRgb(r,g,b));
    private async Task Reload(){if(working)return;working=true;refresh.IsEnabled=false;Render();status.Text="正在读取运行中的角色…";try{snapshot=await owner.ReadLiveBuildAsync();assets=await Task.Run(()=>GameBuildAssets.Load(snapshot.Skills.Select(x=>x.Key).Concat(LiveBuild.GemKeys)));status.Text="当前角色已读取。灰色退点按钮的原因可在提示中查看。";}catch(Exception e){snapshot=null;status.Text=e.Message;}finally{working=false;refresh.IsEnabled=true;Render();}}
    private void Render(){
        attributes.Children.Clear();skills.Children.Clear();if(snapshot is null){summary.Text="请进入游戏地图后刷新";return;}var s=snapshot;summary.Text=$"{s.Character} · 等级 {s.Attributes[7]}     属性点 {s.Attributes[5]}    技能点 {s.Attributes[6]}    历练 {s.Credits} / 6    已交付契约 {s.Contracts}";
        for(int i=0;i<5;i++){int at=i;var row=new DockPanel{Margin=new Thickness(0,0,0,14)};var minus=new Button{Content="−",Width=30,Height=30,Padding=new Thickness(0),FontSize=18,IsEnabled=!working&&s.Attributes[i]>s.Baseline[i]&&s.AttributeReasons[i].Length==0&&s.SafePlace&&s.Credits>0};AutomationProperties.SetName(minus,"退回"+BuildEditor.AttributeLabels[i]);minus.ToolTip=s.AttributeReasons[i].Length>0?Reason(s.AttributeReasons[i]):!s.SafePlace?"请回到城镇或马车营地":s.Credits<1?"请完成并交付契约，获得历练机会":"选择材料配方，退回 1 属性点";ToolTipService.SetShowOnDisabled(minus,true);DockPanel.SetDock(minus,Dock.Right);row.Children.Add(minus);var value=new TextBlock{Text=s.Attributes[i].ToString(),Width=45,VerticalAlignment=VerticalAlignment.Center,TextAlignment=TextAlignment.Center,FontSize=18};DockPanel.SetDock(value,Dock.Right);row.Children.Add(value);row.Children.Add(new TextBlock{Text=BuildEditor.AttributeLabels[i],VerticalAlignment=VerticalAlignment.Center,FontSize=15});attributes.Children.Add(row);minus.Click+=async(_,_)=>await Refund(new(true,at,-1),BuildEditor.AttributeLabels[at]);}RenderSkills();
    }
    private void RenderSkills(){
        skills.Children.Clear();if(snapshot is null)return;string filter=category.SelectedItem as string??"全部技能";
        foreach(var skill in snapshot.Skills){var item=assets.Get(skill.Key);if(!string.IsNullOrWhiteSpace(skill.Name))item=item with{Name=skill.Name};if((learnedOnly.IsChecked==true&&!skill.Learned)||(filter!="全部技能"&&filter!=Group(skill.Category))||(!string.IsNullOrWhiteSpace(search.Text)&&!item.Name.Contains(search.Text,StringComparison.OrdinalIgnoreCase)&&!skill.Key.Contains(search.Text,StringComparison.OrdinalIgnoreCase)))continue;string reason=skill.Reason.StartsWith("o_")?"请先退回："+(snapshot.Skills.FirstOrDefault(x=>x.Key==skill.Reason)?.Name??assets.Get(skill.Reason).Name):skill.Reason;if(reason.Length==0&&!snapshot.SafePlace)reason="请回到城镇或马车营地";if(reason.Length==0&&snapshot.Credits<1)reason="请完成并交付契约，获得历练机会";
            var box=new Border{Width=192,Margin=new Thickness(0,0,8,8),Padding=new Thickness(9),Background=skill.Learned?B(51,47,34):B(35,35,40),BorderBrush=skill.Learned?B(154,125,64):B(60,60,66),BorderThickness=new Thickness(1),ToolTip=reason.Length>0?reason:"选择材料配方，退回 1 技能点"};var row=new DockPanel();box.Child=row;var minus=new Button{Content="−",Width=28,Height=29,Padding=new Thickness(0),FontSize=18,IsEnabled=!working&&skill.Learned&&reason.Length==0,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(minus,Dock.Right);row.Children.Add(minus);AutomationProperties.SetName(minus,"退回 "+item.Name);minus.Click+=async(_,_)=>await Refund(new(false,skill.Id,-1),item.Name);
            if(item.Icon is not null){var image=new Image{Source=item.Icon,Width=34,Height=34,Margin=new Thickness(0,0,8,0)};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.NearestNeighbor);DockPanel.SetDock(image,Dock.Left);row.Children.Add(image);}var text=new StackPanel();text.Children.Add(new TextBlock{Text=item.Name,TextWrapping=TextWrapping.Wrap,Foreground=skill.Learned?Brushes.Wheat:Brushes.Gainsboro});text.Children.Add(new TextBlock{Text=Group(skill.Category)+" · "+(skill.Learned?"已学习":"未学习"),FontSize=11,Foreground=Brushes.Silver,Margin=new Thickness(0,4,0,0)});row.Children.Add(text);skills.Children.Add(box);
        }
    }
    private string Reason(string reason)=>reason.StartsWith("o_")?"请先退回："+(snapshot?.Skills.FirstOrDefault(x=>x.Key==reason)?.Name??assets.Get(reason).Name):reason;
    private int? ChooseRecipe(LiveBuildSnapshot s,bool attribute,string name){
        int? selected=null;
        var dialog=new Window{Title="选择退点材料",Owner=this,Width=570,Height=530,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=B(25,25,29),Foreground=Brushes.Gainsboro,FontFamily=FontFamily};
        var panel=new StackPanel{Margin=new Thickness(22)};dialog.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(new TextBlock{Text=$"退回「{name}」1 点",FontSize=21,Foreground=Brushes.Wheat,Margin=new Thickness(0,0,0,12)});
        panel.Children.Add(new TextBlock{Text=$"历练机会 {s.Credits} / 6 → {s.Credits-1} / 6\n请选择一种配方；以下为 I 主背包中的可用数量。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
        foreach(var recipe in LiveBuild.Options(attribute)){
            string missing=LiveBuild.Missing(s,recipe);
            var button=new Button{Margin=new Thickness(0,0,0,10),Padding=new Thickness(12),HorizontalContentAlignment=HorizontalAlignment.Left,IsEnabled=missing.Length==0};
            var rows=new StackPanel();button.Content=rows;rows.Children.Add(new TextBlock{Text=LiveBuild.Cost(recipe),FontSize=15});
            rows.Children.Add(new TextBlock{Text=string.Join("  ",recipe.Materials.Select((n,i)=>(n,i)).Where(x=>x.n>0).Select(x=>$"{LiveBuild.GemNames[x.i]} {s.Gems[x.i]}/{x.n}")),Margin=new Thickness(0,5,0,0)});
            rows.Children.Add(new TextBlock{Text=missing.Length>0?"缺少："+missing:"材料齐全 · 点击选择",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0)});
            panel.Children.Add(button);button.Click+=(_,_)=>{selected=recipe.Id;dialog.DialogResult=true;};
        }
        var cancel=new Button{Content="取消",Padding=new Thickness(15,8,15,8),HorizontalAlignment=HorizontalAlignment.Right,IsCancel=true};panel.Children.Add(cancel);dialog.ShowDialog();return selected;
    }
    private async Task Refund(PointRefund refund,string name){
        if(working||snapshot is null)return;var before=snapshot;
        int? chosen=ChooseRecipe(before,refund.Attribute,name);if(chosen is null)return;refund=refund with{Recipe=chosen.Value};
        if(MessageBox.Show(this,$"退回「{name}」1 点？\n\n消耗：{LiveBuild.Cost(LiveBuild.Recipes[refund.Recipe])}\n历练机会：{before.Credits} → {before.Credits-1}\n不扣金币。先保存并备份，成功后再次保存。\n可在游戏原版界面重新加点。","确认消耗材料并退回 1 点",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        working=true;refresh.IsEnabled=false;Render();status.Text="正在保存保险备份并核对材料，请稍候…";try{string result=await owner.RefundPointAsync(before,refund);snapshot=await owner.ReadLiveBuildAsync();status.Text=result;}catch(Exception e){snapshot=null;status.Text=e.Message;}finally{working=false;refresh.IsEnabled=true;Render();Activate();}
    }
}
