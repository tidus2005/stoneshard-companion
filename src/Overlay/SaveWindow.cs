using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StoneshardCompanion;

public sealed class SaveWindow : Window
{
    private readonly MainWindow coordinator;
    private readonly ListBox history=new(){Height=250,Background=new SolidColorBrush(Color.FromRgb(30,28,37)),Foreground=Brushes.Wheat,DisplayMemberPath="Display"};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
    private readonly TextBlock latest=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver};
    private readonly ProgressBar progress=new(){Height=5,Margin=new Thickness(0,8,0,8),Visibility=Visibility.Collapsed};
    private readonly StackPanel confirmation=new(){Visibility=Visibility.Collapsed};
    private readonly TextBlock confirmText=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Wheat,Margin=new Thickness(0,10,0,8)};
    private readonly CheckBox inMenu=new(){Content="游戏已停在主菜单 / 死亡界面，或已关闭",Foreground=Brushes.Wheat};
    private readonly List<Button> operations=[];
    private readonly Button confirm=new();
    private SaveArchive? pending;
    public SaveWindow(MainWindow owner)
    {
        coordinator=owner;Title="晶石助手 · 存档管理";Width=840;Height=680;MinWidth=520;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=new SolidColorBrush(Color.FromRgb(25,23,31));Foreground=Brushes.Wheat;FontFamily=new FontFamily("Microsoft YaHei UI");
        var body=new Grid{Margin=new Thickness(22)};Content=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        SizeChanged+=(_,_)=>history.Height=Math.Clamp(ActualHeight-400,140,450);
        for(int i=0;i<6;i++)body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var header=new StackPanel();header.Children.Add(new TextBlock{Text="存档备份",FontSize=24});
        header.Children.Add(new TextBlock{Text="备份已保存到磁盘的全部角色与设置；不会保存尚未落盘的游戏进度。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver,Margin=new Thickness(0,8,0,10)});header.Children.Add(latest);body.Children.Add(header);
        var buttons=new WrapPanel{Margin=new Thickness(0,12,0,4)};Grid.SetRow(buttons,1);body.Children.Add(buttons);
        Add(buttons,"一键完整备份",()=>owner.Backup(false));Add(buttons,"刷新最新备份",()=>owner.Backup(true));Add(buttons,"刷新列表",Refresh);
        Add(buttons,"打开备份目录",()=>{try{System.IO.Directory.CreateDirectory(owner.Saves.BackupRoot);Process.Start(new ProcessStartInfo(owner.Saves.BackupRoot){UseShellExecute=true});}catch(Exception e){status.Text=e.Message;}});
        var state=new StackPanel();state.Children.Add(status);state.Children.Add(progress);Grid.SetRow(state,2);body.Children.Add(state);
        Grid.SetRow(history,3);body.Children.Add(history);
        var restoreRow=new WrapPanel{Margin=new Thickness(0,8,0,0)};Grid.SetRow(restoreRow,4);body.Children.Add(restoreRow);
        Add(restoreRow,"还原选中的备份",()=>{
            if(history.SelectedItem is not SaveArchive archive){status.Text="先在列表中选择一个备份。";return;}
            pending=archive;confirmText.Text=$"将还原：{archive.Name}\n还原前会自动备份当前状态，原备份保持不变。文件还原成功不等于游戏已成功读档；运行中热还原曾出现加载失败，本版尚未实机验证。";
            inMenu.IsChecked=false;confirmation.Visibility=Visibility.Visible;confirm.IsEnabled=false;
        });
        confirmation.Children.Add(confirmText);confirmation.Children.Add(inMenu);var confirmButtons=new WrapPanel();confirmation.Children.Add(confirmButtons);
        confirm.Content="确认还原此备份";confirm.Margin=new Thickness(0,0,8,5);confirmButtons.Children.Add(confirm);operations.Add(confirm);
        confirm.Click+=async(_,_)=>{
            if(pending is null||inMenu.IsChecked!=true)return;var selected=pending;pending=null;confirmation.Visibility=Visibility.Collapsed;
            await owner.SaveAsync(SaveOperation.Restore,selected.Path);
        };
        Add(confirmButtons,"取消",()=>{pending=null;confirmation.Visibility=Visibility.Collapsed;});
        inMenu.Checked+=(_,_)=>confirm.IsEnabled=!owner.Saves.Busy;inMenu.Unchecked+=(_,_)=>confirm.IsEnabled=false;
        Grid.SetRow(confirmation,5);body.Children.Add(confirmation);
        Closing+=(_,e)=>{if(owner.Saves.Busy){e.Cancel=true;status.Text="存档操作正在进行，请完成后关闭。";}};
        Refresh();
    }
    private Button Add(Panel panel,string text,Action action)
    {
        var b=new Button{Content=text,Margin=new Thickness(0,0,8,5)};b.Click+=(_,_)=>action();panel.Children.Add(b);operations.Add(b);return b;
    }
    public void Refresh()
    {
        bool busy=coordinator.Saves.Busy;status.Text=coordinator.SaveStatus;progress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=busy;
        foreach(var button in operations)button.IsEnabled=!busy;confirm.IsEnabled=!busy&&pending is not null&&inMenu.IsChecked==true;history.IsEnabled=!busy;
        try{
            string? selected=(history.SelectedItem as SaveArchive)?.Path;var items=coordinator.Saves.List();history.ItemsSource=items;
            history.SelectedItem=items.FirstOrDefault(a=>a.Path==selected)??items.FirstOrDefault();var last=items.FirstOrDefault(a=>!a.Safety);
            latest.Text=last is null?"尚无备份":$"最近备份：{last.Modified:yyyy-MM-dd HH:mm:ss} · {last.Name}";
        }catch(Exception e){status.Text="读取备份列表失败："+e.Message;}
    }
}
