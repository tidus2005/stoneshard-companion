using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace StoneshardCompanion;
public sealed class SettingsWindow : Window
{
    private readonly TextBlock status;
    private readonly TextBlock updateStatus=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,8,0,0)};
    public void RefreshUpdate(string text)=>updateStatus.Text=text;
    private readonly List<(CheckBox Control,Func<bool> Value)> checks=[];
    public void RefreshStatus(string text){status.Text=text;foreach(var c in checks)c.Control.IsChecked=c.Value();}
    public SettingsWindow(MainWindow owner)
    {
        Title=$"晶石助手 v{App.Version} · 设置";Width=560;Height=760;MinWidth=440;MinHeight=430;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=new SolidColorBrush(Color.FromRgb(25,23,31));Foreground=Brushes.Wheat;FontFamily=new FontFamily("Microsoft YaHei UI");
        var panel=new StackPanel{Margin=new Thickness(26)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        panel.Children.Add(new TextBlock{Text=$"行旅辅助 · v{App.Version}",FontSize=24,Margin=new Thickness(0,0,0,12)});
        status=new TextBlock{Text=owner.Status,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray};panel.Children.Add(status);
        Check("自动更新（GitHub 正式版；游戏关闭后安装并重启）",()=>owner.Preferences.AutoUpdate,v=>owner.Preferences.AutoUpdate=v);
        updateStatus.Text=owner.Updater.Status;panel.Children.Add(updateStatus);
        Button("检查更新",()=>_=owner.Updater.CheckAsync(true));
        AddText("拖动面板顶部移动位置，拖动四角调整宽高，按钮随形状自动排列。位置与尺寸会保存；仅打开状态栏或物品栏时自动避让；主菜单、断线和退出游戏后仍保留备份入口。");
        Check("物品常显（移动、装备栏和切图后保持）",()=>owner.Preferences.ShowLabels,v=>owner.Preferences.ShowLabels=v);
        Check("方向键自动移动（沿人物所在行或列走到边缘）",()=>owner.Preferences.AutoWalkKeys,v=>owner.Preferences.AutoWalkKeys=v);
        AddText("九宫格的斜向键走到四个角点后停下，中心键走到地图中心。开启方向键自动移动后，轻按普通方向键沿人物当前行或列寻路到边缘；到达边缘后自动点击相邻出口切图。途中再按一次方向键停步，长按不重复。开关会保存，仅在游戏前台且未打开面板时生效。关闭此开关也会停止键盘发起的行走。");
        Check("进入新地图时镜头自动居中",()=>owner.Preferences.AutoCenter,v=>owner.Preferences.AutoCenter=v);
        Check("下次启动游戏沿用倍率",()=>owner.Preferences.RememberSpeed,v=>{owner.Preferences.RememberSpeed=v;owner.Preferences.LastSpeed=owner.PreferredSpeed;});
        AddText("速度按钮点击即按正常 1× → 加速 2× → 急速 3× → 正常循环，不弹出菜单；三种图标对应三个档位。状态栏和背包打开时保持所选速度；暂停菜单、对话和大地图中暂回正常。自动走图沿用原版寻路与回合消耗，到边缘后再跨入相邻地图，进入新地图后结束；手动接管、遇敌、打开面板或切到后台时结束。不可达目标或出口会停止并提示。左侧中心键让角色走到地图中央，行走中点击任一方向键停步。");
        AddText("角色属性与饲料",16);
        Check("左侧显示半透明角色属性面板",()=>owner.Preferences.ShowStats,v=>owner.Preferences.ShowStats=v);
        AddText("属性面板可拖动标题移动；全部／特别关注切换，悬停点星标保存关注。悬停数值查看原生基础值和来源；不可确认的来源不会伪造。打开游戏原生面板时自动避让。");
        Button("马车饲料：选择材料／一键制作",owner.OpenFodder);
        AddText("自动补给",16);
        Check("自动喝水",()=>owner.Preferences.AutoDrink,v=>owner.Preferences.AutoDrink=v);
        var threshold=new TextBlock{Text=$"口渴达到 {owner.Preferences.DrinkThreshold:0}% 时喝水",Margin=new Thickness(0,12,0,8)};panel.Children.Add(threshold);
        var slider=new Slider{Minimum=10,Maximum=80,TickFrequency=5,IsSnapToTickEnabled=true,Value=owner.Preferences.DrinkThreshold};
        slider.ValueChanged+=(_,_)=>{owner.Preferences.DrinkThreshold=slider.Value;threshold.Text=$"口渴达到 {slider.Value:0}% 时喝水";owner.SavePreferences();};panel.Children.Add(slider);
        Check("自动火把：保持点亮，耗尽后选用随身备用火把",()=>owner.Preferences.AutoTorch,v=>owner.Preferences.AutoTorch=v);
        AddText("自动补给仅在前台、安全且空闲时执行；出现敌人、受伤、移动、菜单或加载时暂停。保留原版物品和回合消耗，操作未确认时暂停自动功能。手动切换火把会关闭自动保持点亮。");
        AddText("Ctrl + Alt + 1 / 2 / 3 / 4　选择速度\nCtrl + Alt + H　开合面甲\nCtrl + Alt + C / R　镜头居中 / 角色视角\nCtrl + Alt + L　物品常显\nCtrl + Alt + W / T　喝水 / 火把\nCtrl + Alt + 方向键　跨入对应方向的相邻地图\nCtrl + Alt + Home　角色走到地图中心\nCtrl + Alt + End　停止行走\nCtrl + Alt + B　一键备份\nCtrl + Alt + S　停止并恢复正常\nCtrl + Alt + O　隐藏 / 显示面板");
        Button("存档管理",owner.OpenSaves);Button("一键备份已落盘的存档",()=>owner.Backup(false));Button("恢复默认面板位置与大小",owner.ResetLayout);Button("停止并恢复正常",owner.Reset);Button("退出助手",owner.RequestExit);
        AddText("图标：Game-icons.net · CC BY 3.0，作者与来源见随附 ATTRIBUTION.md。",11);
        void AddText(string text,int size=13)=>panel.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=size,LineHeight=23,Margin=new Thickness(0,14,0,8),Foreground=Brushes.LightGray});
        void Button(string title,Action action){var b=new Button{Content=title,Margin=new Thickness(0,8,0,0)};b.Click+=(_,_)=>action();panel.Children.Add(b);}
        void Check(string text,Func<bool> value,Action<bool> change){var check=new CheckBox{Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap},IsChecked=value(),Foreground=Brushes.Wheat,Margin=new Thickness(0,12,0,0)};check.Click+=(_,_)=>{change(check.IsChecked==true);owner.SavePreferences();};checks.Add((check,value));panel.Children.Add(check);}
    }
}
