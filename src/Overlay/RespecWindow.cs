using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace StoneshardCompanion;
public sealed class RespecWindow:Window
{
    private readonly MainWindow owner;
    private readonly TextBlock details=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Wheat,FontSize=14,LineHeight=24};
    private readonly Button refresh=new(){Content="刷新存档预览",Margin=new Thickness(0,16,0,8)};
    private readonly Button apply=new(){Content="确认支付 2000 克朗并洗点",IsEnabled=false,Margin=new Thickness(0,8,0,0)};
    private RespecPreview? preview;
    private bool working;
    public RespecWindow(MainWindow owner){
        this.owner=owner;Title="付费洗点 · 2000 克朗";Width=550;Height=560;MinWidth=450;MinHeight=430;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=new SolidColorBrush(Color.FromRgb(25,23,31));FontFamily=new FontFamily("Microsoft YaHei UI");
        var panel=new StackPanel{Margin=new Thickness(26)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(new TextBlock{Text="重新尝试一套 build",FontSize=23,Foreground=Brushes.Wheat});
        panel.Children.Add(new TextBlock{Text="先在游戏里保存并退出到桌面，再刷新预览。保留等级、经验、装备、基础动作、角色天赋和书籍解锁；返还加点供重新分配。只处理最后使用的退出存档。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver,Margin=new Thickness(0,14,0,16)});
        panel.Children.Add(details);panel.Children.Add(refresh);panel.Children.Add(apply);
        refresh.Click+=async(_,_)=>await RefreshAsync();apply.Click+=async(_,_)=>await ApplyAsync();Loaded+=async(_,_)=>await RefreshAsync();
        Closing+=(_,e)=>{if(working)e.Cancel=true;};
    }
    private async Task RefreshAsync(){
        if(working)return;working=true;refresh.IsEnabled=apply.IsEnabled=false;preview=null;
        try{
            PaidRespec.RequireGameClosed();preview=await Task.Run(()=>PaidRespec.Preview(owner.Saves.SaveRoot));
            details.Text=$"角色：{preview.Character} · 等级 {preview.Level}（保留）\n存档：{preview.Slot}\n克朗：{preview.Crowns} → {preview.Crowns-PaidRespec.Price}\n返还属性点：{preview.RefundedAttributes}，可分配合计 {preview.AvailableAttributes}\n返还技能点：{preview.RefundedSkills}，可分配合计 {preview.AvailableSkills}\n初始属性：{preview.Summary}\n\n确认后先完整备份。读档后重新分配，重置技能对应的快捷栏会清空。";
            apply.IsEnabled=true;
        }catch(Exception e){details.Text=e.Message;}
        finally{working=false;refresh.IsEnabled=true;}
    }
    private async Task ApplyAsync(){
        if(working||preview is null||owner.Saves.Busy)return;
        if(MessageBox.Show(this,$"为 {preview.Character} 的这个退出存档支付 2000 克朗？\n返还 {preview.RefundedAttributes} 属性点与 {preview.RefundedSkills} 技能点，等级 {preview.Level} 不变。\n\n将先创建可恢复的完整备份。", "确认付费洗点",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        working=true;refresh.IsEnabled=apply.IsEnabled=false;
        try{
            var result=await owner.Saves.RespecAsync(preview);preview=null;
            details.Text=$"洗点完成。剩余 {result.CrownsRemaining} 克朗。\n可分配属性点 {result.AttributePoints}，技能点 {result.SkillPoints}。\n\n启动游戏并继续这个退出存档即可重新加点。\n\n操作前保险备份：\n{result.Backup}";
        }catch(Exception e){preview=null;details.Text=e.Message+"\n请刷新预览再核对。";}
        finally{working=false;refresh.IsEnabled=true;}
    }
}
