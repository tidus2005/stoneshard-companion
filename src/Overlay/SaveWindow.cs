using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StoneshardCompanion;

public sealed class SaveWindow : Window
{
    private readonly MainWindow coordinator;
    private readonly ListBox history=new(){SelectionMode=SelectionMode.Extended,Background=new SolidColorBrush(Color.FromRgb(30,28,37)),Foreground=Brushes.Wheat,DisplayMemberPath="Display"};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
    private readonly TextBlock latest=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver};
    private readonly TextBlock quickTarget=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGreen,Margin=new Thickness(0,6,0,6)};
    private bool quickRequested;
    private readonly TextBox locations=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Background=Brushes.Transparent,Foreground=Brushes.Silver,BorderThickness=new Thickness(0),Margin=new Thickness(0,6,0,0)};
    private readonly ProgressBar progress=new(){Height=5,Margin=new Thickness(0,8,0,8),Visibility=Visibility.Collapsed};
    private readonly StackPanel confirmation=new(){Visibility=Visibility.Collapsed};
    private readonly TextBlock confirmText=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Wheat,Margin=new Thickness(0,10,0,8)};
    private readonly CheckBox inMenu=new(){Content="游戏已停在主菜单 / 死亡界面，或已关闭",Foreground=Brushes.Wheat};
    private readonly List<Button> operations=[];
    private readonly Button confirm=new();
    private SaveArchive? pending;
    private SaveArchive[] pendingDelete=[];
    private bool loadingHistory,historyQueued,closed;
    private readonly Image preview=new(){Width=220,MaxHeight=190,Stretch=Stretch.Uniform};
    private readonly TextBlock previewInfo=new(){Text="选中备份查看存档截图",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver,Margin=new Thickness(8)};
    private int previewGeneration;
    public SaveWindow(MainWindow owner)
    {
        coordinator=owner;Title=$"晶石助手 v{App.Version} · 存档管理";Width=840;Height=680;MinWidth=520;MinHeight=580;Topmost=true;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=new SolidColorBrush(Color.FromRgb(25,23,31));Foreground=Brushes.Wheat;FontFamily=new FontFamily("Microsoft YaHei UI");
        var body=new Grid{Margin=new Thickness(22)};Content=body;
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/StoneshardCompanion;component/PanelTheme.xaml",UriKind.Relative)});

        for(int i=0;i<6;i++)body.RowDefinitions.Add(new RowDefinition{Height=i==5?new GridLength(1,GridUnitType.Star):GridLength.Auto});
        var header=new StackPanel();header.Children.Add(new TextBlock{Text="存档备份",FontSize=24});
        header.Children.Add(new TextBlock{Text="备份已保存到磁盘的全部角色与设置；不会保存尚未落盘的游戏进度。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Silver,Margin=new Thickness(0,8,0,10)});header.Children.Add(latest);body.Children.Add(header);
        header.Children.Add(quickTarget);
        header.Children.Add(new Expander{Header="存档与备份路径",Content=locations,Foreground=Brushes.Silver});
        header.Children.Add(new TextBlock{Text="手动保留备份 = 你主动保留的进度；当前状态 / latest 与还原前保险可能是死亡或读档前状态。旧版文件无法仅凭名字判断来源，请看截图。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkKhaki});
        var buttons=new WrapPanel{Margin=new Thickness(0,12,0,4)};Grid.SetRow(buttons,1);body.Children.Add(buttons);
        Add(buttons,"保留一份手动备份",()=>owner.Backup(false));Add(buttons,"更新当前状态（latest）",()=>owner.Backup(true));Add(buttons,"刷新列表",()=>Refresh());
        Add(buttons,"快速读档",PrepareQuickRestore);
        Add(buttons,"打开备份目录",()=>{try{System.IO.Directory.CreateDirectory(owner.Saves.BackupRoot);Process.Start(new ProcessStartInfo(owner.Saves.BackupRoot){UseShellExecute=true});}catch(Exception e){status.Text=e.Message;}});
        Add(buttons,"本地备用备份",()=>{try{System.IO.Directory.CreateDirectory(SaveEngine.DefaultStagingRoot);Process.Start(new ProcessStartInfo(SaveEngine.DefaultStagingRoot){UseShellExecute=true});}catch(Exception e){status.Text=e.Message;}});
        Add(buttons,"选择备份目录",()=>{
            var picker=new Microsoft.Win32.OpenFolderDialog{Title="选择备份目录（原有备份保留在原目录）",Multiselect=false};
            if(picker.ShowDialog(this)!=true)return;
            try{coordinator.ChangeBackupFolder(picker.FolderName);pending=null;pendingDelete=[];confirmation.Visibility=Visibility.Collapsed;inMenu.IsChecked=false;Refresh();}
            catch(Exception e){status.Text="备份目录未切换："+e.Message;}
        });
        var state=new StackPanel();state.Children.Add(status);state.Children.Add(progress);Grid.SetRow(state,2);body.Children.Add(state);
        var copyStatus=new Button{Content="复制状态与路径",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,6)};
        copyStatus.Click+=(_,_)=>{try{Clipboard.SetText($"{Title}\n{status.Text}\n{latest.Text}\n{locations.Text}");}catch(System.Runtime.InteropServices.ExternalException){status.Text="剪贴板正在被占用，请稍后重试。";}};
        state.Children.Add(copyStatus);
        var archiveRow=new Grid();archiveRow.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});archiveRow.ColumnDefinitions.Add(new(){Width=new GridLength(240)});
        archiveRow.Children.Add(history);var previewPanel=new StackPanel{Margin=new Thickness(8)};previewPanel.Children.Add(preview);previewPanel.Children.Add(previewInfo);Grid.SetColumn(previewPanel,1);archiveRow.Children.Add(previewPanel);Grid.SetRow(archiveRow,5);body.Children.Add(archiveRow);
        history.SelectionChanged+=async(_,_)=>await LoadPreview();
        var restoreRow=new WrapPanel{Margin=new Thickness(0,8,0,0)};Grid.SetRow(restoreRow,3);body.Children.Add(restoreRow);
        Add(restoreRow,"还原选中的备份",()=>{
            if(history.SelectedItems.Count!=1||history.SelectedItem is not SaveArchive archive){status.Text="还原时请只选择一个备份。";return;}
            PrepareRestore(archive);
        });
        confirmation.Children.Add(confirmText);confirmation.Children.Add(inMenu);var confirmButtons=new WrapPanel();confirmation.Children.Add(confirmButtons);
        confirm.Content="确认还原此备份";confirm.Margin=new Thickness(0,0,8,5);confirmButtons.Children.Add(confirm);operations.Add(confirm);
        confirm.Click+=async(_,_)=>{
            if(inMenu.IsChecked!=true)return;
            if(pendingDelete.Length>0){
                var batch=pendingDelete;
                if(MessageBox.Show(this,$"再次确认：将 {batch.Length} 个备份及校验文件移入回收站。\n不会删除游戏存档。\n\n"+string.Join("\n",batch.Take(6).Select(a=>a.Display)),"二次确认删除",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
                pendingDelete=[];confirmation.Visibility=Visibility.Collapsed;
                var task=owner.Saves.DeleteAsync(batch);Refresh(false);
                try{int removed=await task;Refresh();status.Text=$"已将 {removed} 个备份移入回收站。";}
                catch(Exception ex){Refresh();status.Text="删除未全部完成，请刷新检查："+ex.Message;}
                return;
            }
            if(pending is null)return;var selected=pending;pending=null;pendingDelete=[];confirmation.Visibility=Visibility.Collapsed;
            await owner.SaveAsync(SaveOperation.Restore,selected.Path);
        };
        Add(confirmButtons,"取消",()=>{pending=null;pendingDelete=[];confirmation.Visibility=Visibility.Collapsed;});
        Add(restoreRow,"全选",()=>history.SelectAll());
        Add(restoreRow,"取消选择",()=>history.UnselectAll());
        Add(restoreRow,"删除所选…",()=>{
            pendingDelete=history.SelectedItems.Cast<SaveArchive>().ToArray();pending=null;
            if(pendingDelete.Length==0){status.Text="先选择要删除的备份（支持 Ctrl / Shift 多选）。";return;}
            confirmText.Text=$"准备删除 {pendingDelete.Length} 个备份，共 {pendingDelete.Sum(a=>a.Bytes)/1048576.0:0.#} MB。请先核对所选列表；确认后仍有一次二次确认。";
            inMenu.Content="我已核对所选备份，确认这些不再需要";inMenu.IsChecked=false;
            confirm.Content="继续核对删除…";confirmation.Visibility=Visibility.Visible;
        });
        inMenu.Checked+=(_,_)=>confirm.IsEnabled=!owner.Saves.Busy;inMenu.Unchecked+=(_,_)=>confirm.IsEnabled=false;
        Grid.SetRow(confirmation,4);body.Children.Add(confirmation);
        Closing+=(_,e)=>{if(owner.Saves.Busy){e.Cancel=true;status.Text="存档操作正在进行，请完成后关闭。";}};
        Closed+=(_,_)=>closed=true;
        Refresh();
    }
    public void PrepareQuickRestore()
    {
        if(coordinator.Saves.Busy||closed)return;
        quickRequested=true;_=ReloadHistory();
    }
    private void PrepareRestore(SaveArchive archive)
    {
        pendingDelete=[];pending=archive;confirm.Content="确认还原此备份";
        inMenu.Content="游戏已停在主菜单 / 死亡界面，或已关闭";
        confirmText.Text=$"将还原：{archive.Name}\n备份时间：{archive.Modified:yyyy-MM-dd HH:mm:ss}\n还原前自动保留当前状态。还原后请在游戏内重新读档；不要继续当前游戏后再保存。";
        inMenu.IsChecked=false;confirmation.Visibility=Visibility.Visible;confirm.IsEnabled=false;
    }
    private async Task LoadPreview(){
        int generation=++previewGeneration;preview.Source=null;
        if(history.SelectedItem is not SaveArchive archive)return;previewInfo.Text="读取存档截图…";
        try{
            var data=await Task.Run(()=>SavePreviews.ReadLatest(archive.Path));if(closed||generation!=previewGeneration)return;
            if(data is null){previewInfo.Text="此备份没有游戏原生截图";return;}
            using var stream=new System.IO.MemoryStream(data.Image);var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=440;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();preview.Source=bitmap;
            previewInfo.Text=$"{archive.Kind}\n游戏存档时间\n{data.SavedAt.LocalDateTime:MM-dd HH:mm:ss}\n{data.Slot.Replace("/preview.png","")}\n截图时间可能早于备份时间。";
        }catch(Exception ex){if(!closed&&generation==previewGeneration)previewInfo.Text="无法读取截图；备份仍可选择。";UserPreferences.Log("save-preview-failed "+ex);}
    }
    private Button Add(Panel panel,string text,Action action)
    {
        var b=new Button{Content=text,Margin=new Thickness(0,0,8,5)};b.Click+=(_,_)=>action();panel.Children.Add(b);operations.Add(b);return b;
    }
    public void Refresh(bool reloadHistory=true)
    {
        if(closed)return;
        bool busy=coordinator.Saves.Busy;status.Text=coordinator.SaveStatus;progress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=busy;
        locations.Text=$"存档：{coordinator.Saves.SaveRoot}\n备份：{coordinator.Saves.BackupRoot}";
        foreach(var button in operations)button.IsEnabled=!busy;confirm.IsEnabled=!busy&&(pending is not null||pendingDelete.Length>0)&&inMenu.IsChecked==true;history.IsEnabled=!busy;
        if(reloadHistory&&!busy)_=ReloadHistory();
    }
    private async Task ReloadHistory()
    {
        if(loadingHistory){historyQueued=true;return;}
        historyQueued=false;loadingHistory=true;var service=coordinator.Saves;latest.Text="正在读取备份列表…";
        try{
            var items=await service.ListAsync();
            if(closed||service!=coordinator.Saves)return;
            var target=coordinator.ResolveQuickBackup(items);
            quickTarget.Text=target is not null?$"★ 快速读档目标 · 手动备份 {target.Modified:yyyy-MM-dd HH:mm:ss}\n{target.Name}":coordinator.Preferences.QuickRestoreArchive is null?"快速读档：尚无手动备份，请先快速备份。":"快速读档目标不在当前目录或已删除。请切回原备份目录，或重新快速备份；不会改用 latest。";
            string? selected=(history.SelectedItem as SaveArchive)?.Path;history.ItemsSource=items;
            history.SelectedItem=items.FirstOrDefault(a=>a.Path==selected)??target??items.FirstOrDefault();var last=items.FirstOrDefault(a=>!a.Safety);
            if(quickRequested){quickRequested=false;pending=null;pendingDelete=[];confirmation.Visibility=Visibility.Collapsed;if(target is not null){history.SelectedItem=target;history.ScrollIntoView(target);PrepareRestore(target);}else status.Text=quickTarget.Text;}
            latest.Text=last is null?"尚无备份":$"最近备份：{last.Modified:yyyy-MM-dd HH:mm:ss} · {last.Name}";
        }catch(Exception e){if(!closed&&service==coordinator.Saves)latest.Text="读取备份列表失败："+e.Message;}
        finally{loadingHistory=false;if(historyQueued&&!closed&&!coordinator.Saves.Busy){historyQueued=false;_=ReloadHistory();}}
    }
}
