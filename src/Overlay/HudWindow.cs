using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace StoneshardCompanion;

public sealed class HudWindow : Window
{
    private readonly MainWindow coordinator;
    private readonly UniformGrid actions=new();
    private readonly List<(Button Button,FrameworkElement Icon,TextBlock Label,uint Capability)> cells=[];
    private readonly TextBlock[] values=new TextBlock[4];
    private readonly TextBlock footer=new(){FontSize=10,Foreground=Brushes.Silver,TextTrimming=TextTrimming.CharacterEllipsis};
    private readonly Button speed,drink,torch,labels;
    private readonly Button walkKeys=new(){FontSize=11,Padding=new Thickness(5,0,5,0),Focusable=false,MinWidth=132};
    private readonly List<(Button Button,int Direction)> navigation=[];
    private bool walking;
    private nint handle;
    private double dpi=1,viewportWidth=1920,viewportHeight=1080;
    private int originX,originY;
    private bool dragging;
    private int openMenus;
    public bool IsInteracting=>dragging||openMenus>0;
    private Native.Point dragMouse;
    private Native.Point mouseDownPoint;
    private bool hasMouseDownPoint;
    private HudRect placement,dragStart;
    private static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(170,167,181));
    private static readonly Brush Active=new SolidColorBrush(Color.FromRgb(221,201,152));
    public HudWindow(MainWindow owner)
    {
        coordinator=owner;Title="晶石助手 · 游戏增强";Width=HudGeometry.DefaultWidth;Height=HudGeometry.DefaultHeight;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
        Topmost=true;ShowInTaskbar=App.UiTestMode;ShowActivated=false;FontFamily=new FontFamily("Microsoft YaHei UI");UseLayoutRounding=true;SnapsToDevicePixels=true;
        var root=new Grid();Content=root;
        var body=new Grid{Margin=new Thickness(10)};
        foreach(var h in new[]{new GridLength(22),new GridLength(26),new GridLength(1,GridUnitType.Star),new GridLength(18)})body.RowDefinitions.Add(new RowDefinition{Height=h});
        root.Children.Add(new Border{Child=body,Background=new SolidColorBrush(Color.FromArgb(246,24,23,30)),BorderBrush=new SolidColorBrush(Color.FromRgb(109,96,116)),BorderThickness=new Thickness(2)});
        var grip=MakeThumb(HudCorner.Move,Cursors.SizeAll);
        grip.Template=TextThumbTemplate("⋮⋮   行旅辅助",false);grip.ToolTip="拖动此处移动面板；拖动四角改变大小";
        var header=new DockPanel();DockPanel.SetDock(walkKeys,Dock.Right);header.Children.Add(walkKeys);header.Children.Add(grip);body.Children.Add(header);
        AutomationProperties.SetName(walkKeys,"方向键自动移动");walkKeys.Click+=(_,_)=>owner.ToggleWalkKeys();
        var stats=new UniformGrid{Columns=4};Grid.SetRow(stats,1);body.Children.Add(stats);
        string[] names=["饥饿","口渴","疼痛","迷醉"],icons=["meat","water-flask","broken-heart","poison-bottle"];
        for(int i=0;i<4;i++){
            var stat=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,ToolTip=names[i]};
            stat.Children.Add(HudIcon.Create(icons[i],16));values[i]=new TextBlock{Text="—",FontSize=12,Foreground=Muted,Margin=new Thickness(4,0,0,0)};stat.Children.Add(values[i]);stats.Children.Add(stat);
        }
        var actionArea=new Grid();Grid.SetRow(actionArea,2);body.Children.Add(actionArea);
        actionArea.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(HudGeometry.NavigationSize+HudGeometry.NavigationGap)});
        actionArea.ColumnDefinitions.Add(new ColumnDefinition());
        var compass=new Grid{Width=HudGeometry.NavigationSize,Height=HudGeometry.NavigationSize,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
        for(int i=0;i<3;i++){compass.RowDefinitions.Add(new RowDefinition());compass.ColumnDefinitions.Add(new ColumnDefinition());}
        foreach(var (direction,symbol,name,row,column) in new[]{(6,"↖","走到左上角",0,0),(1,"↑","向上跨图",0,1),(7,"↗","走到右上角",0,2),(3,"←","向左跨图",1,0),(5,"◎","走到地图中心",1,1),(4,"→","向右跨图",1,2),(8,"↙","走到左下角",2,0),(2,"↓","向下跨图",2,1),(9,"↘","走到右下角",2,2)}){
            var button=new Button{Content=symbol,FontSize=22,Padding=new Thickness(0),Margin=new Thickness(1),ToolTip=name};
            AutomationProperties.SetName(button,name);button.Click+=(_,_)=>coordinator.Walk(walking?0:direction);
            Grid.SetRow(button,row);Grid.SetColumn(button,column);compass.Children.Add(button);navigation.Add((button,direction));
        }
        actionArea.Children.Add(compass);Grid.SetColumn(actions,1);actionArea.Children.Add(actions);
        Add("visored-helm","面甲","开合面甲 · Ctrl+Alt+H",4,()=>owner.RunAction(EngineCommand.Visor));
        drink=Add("water-flask","喝水","一键喝水 · 右键设置自动喝水",16,()=>owner.RunAction(EngineCommand.Drink));
        torch=Add("torch","火把","切换火把 · 右键设置自动保持点亮",32,()=>owner.RunAction(EngineCommand.Torch));
        labels=Add("eye-shield","常显","物品常显 · Ctrl+Alt+L",0,owner.ToggleLabels);
        Add("eye-target","地图中心","镜头移到地图中心 · Ctrl+Alt+C",2,()=>owner.RunAction(EngineCommand.Center));
        Add("crosshair","角色视角","镜头回到角色 · Ctrl+Alt+R",2,()=>owner.RunAction(EngineCommand.Player));
        speed=Add("fast-forward-button","1×","选择速度",0,()=>OpenSpeed());
        Add("save-backup","备份","一键备份已落盘的存档",0,()=>owner.Backup(false));
        Add("save-history","存档","查看历史备份与还原",0,owner.OpenSaves);
        Add("gears","设置","设置与快捷键",0,owner.OpenSettings);
        Add("pause-button","停止","停止行走、加速、常显和自动补给 · Ctrl+Alt+S",0,owner.Reset);
        Add("return-arrow","收起","隐藏面板 · Ctrl+Alt+O 恢复",0,owner.ToggleFold);
        AddSupplyMenu(drink,true);AddSupplyMenu(torch,false);
        Grid.SetRow(footer,3);body.Children.Add(footer);
        foreach(var (corner,h,v,cursor) in new[]{
            (HudCorner.TopLeft,HorizontalAlignment.Left,VerticalAlignment.Top,Cursors.SizeNWSE),
            (HudCorner.TopRight,HorizontalAlignment.Right,VerticalAlignment.Top,Cursors.SizeNESW),
            (HudCorner.BottomLeft,HorizontalAlignment.Left,VerticalAlignment.Bottom,Cursors.SizeNESW),
            (HudCorner.BottomRight,HorizontalAlignment.Right,VerticalAlignment.Bottom,Cursors.SizeNWSE)}){
            var thumb=MakeThumb(corner,cursor);thumb.Width=14;thumb.Height=14;thumb.HorizontalAlignment=h;thumb.VerticalAlignment=v;
            thumb.Template=TextThumbTemplate("◆",true);thumb.ToolTip="拖动调整面板宽高，内容自动排列";root.Children.Add(thumb);
        }
        actions.SizeChanged+=(_,_)=>Reflow();
        SourceInitialized+=(_,_)=>{
            handle=new WindowInteropHelper(this).Handle;long style=Native.GetWindowLongPtr(handle,-20).ToInt64();
            Native.SetWindowLongPtr(handle,-20,(nint)(App.UiTestMode?(style|0x08000000)&~0x80L:style|0x08000000|0x80));
            HwndSource.FromHwnd(handle).AddHook((nint h,int msg,nint w,nint l,ref bool done)=>{
                if(msg==0x201){
                    // Use the queued mouse-down coordinates, not the cursor's
                    // later position when WPF processes a fast drag.
                    mouseDownPoint=new Native.Point{X=(short)((long)l&0xffff),Y=(short)(((long)l>>16)&0xffff)};
                    hasMouseDownPoint=Native.ClientToScreen(h,ref mouseDownPoint);
                }
                if(msg==0x21){done=true;return 3;}return 0;
            });
        };
    }
    private static ControlTemplate TextThumbTemplate(string text,bool corner)
    {
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,Brushes.Transparent);
        var label=new FrameworkElementFactory(typeof(TextBlock));label.SetValue(TextBlock.TextProperty,text);label.SetValue(TextBlock.ForegroundProperty,Muted);label.SetValue(TextBlock.FontSizeProperty,corner?9.0:11.0);label.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);
        border.AppendChild(label);return new ControlTemplate(typeof(Thumb)){VisualTree=border};
    }
    private Thumb MakeThumb(HudCorner corner,Cursor cursor)
    {
        var thumb=new Thumb{Cursor=cursor,Focusable=false};
        thumb.DragStarted+=(_,_)=>{dragging=true;dragStart=placement;if(hasMouseDownPoint)dragMouse=mouseDownPoint;else Native.GetCursorPos(out dragMouse);hasMouseDownPoint=false;if(App.UiTestMode)UserPreferences.Log($"drag-start {corner} {placement}");};
        void ApplyDrag(){
            if(!Native.GetCursorPos(out var mouse))return;
            placement=HudGeometry.Drag(dragStart,corner,(mouse.X-dragMouse.X)/dpi,(mouse.Y-dragMouse.Y)/dpi,viewportWidth,viewportHeight);ApplyPlacement();
        }
        thumb.DragDelta+=(_,_)=>ApplyDrag();
        thumb.DragCompleted+=(_,e)=>{if(e.Canceled){placement=dragStart;ApplyPlacement();}else ApplyDrag();dragging=false;SavePlacement();if(App.UiTestMode)UserPreferences.Log($"drag-end {corner} {placement}");};
        return thumb;
    }
    private Button Add(string icon,string label,string tooltip,uint capability,Action callback)
    {
        var content=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        var visual=HudIcon.Create(icon,23);var caption=new TextBlock{Text=label,Margin=new Thickness(5,0,0,0),VerticalAlignment=VerticalAlignment.Center};
        content.Children.Add(visual);content.Children.Add(caption);
        var button=new Button{Content=content,ToolTip=tooltip,Padding=new Thickness(1),Margin=new Thickness(1)};
        AutomationProperties.SetName(button,label);button.Click+=(_,_)=>callback();actions.Children.Add(button);cells.Add((button,visual,caption,capability));return button;
    }
    private void Reflow()
    {
        var grid=HudGeometry.Grid(actions.ActualWidth,actions.ActualHeight,cells.Count);actions.Columns=grid.Columns;actions.Rows=grid.Rows;
        foreach(var cell in cells){cell.Icon.Width=cell.Icon.Height=Math.Clamp(grid.CellHeight-18,12,30);cell.Label.Visibility=grid.CellWidth>=80?Visibility.Visible:Visibility.Collapsed;cell.Label.FontSize=grid.CellWidth<100?11:12;}
        cells[6].Icon.Visibility=grid.CellWidth>=60?Visibility.Visible:Visibility.Collapsed;
    }
    private void AddSupplyMenu(Button button,bool water)
    {
        var menu=new ContextMenu();var option=new MenuItem{Header=water?"自动喝水":"自动保持火把点亮",IsCheckable=true};
        menu.Opened+=(_,_)=>{openMenus++;option.IsChecked=water?coordinator.Preferences.AutoDrink:coordinator.Preferences.AutoTorch;};menu.Closed+=(_,_)=>openMenus=Math.Max(0,openMenus-1);
        option.Click+=(_,_)=>{if(water)coordinator.Preferences.AutoDrink=option.IsChecked;else coordinator.Preferences.AutoTorch=option.IsChecked;coordinator.SavePreferences();};menu.Items.Add(option);
        var settings=new MenuItem{Header="补给设置与阈值"};settings.Click+=(_,_)=>coordinator.OpenSettings();menu.Items.Add(settings);button.ContextMenu=menu;
    }
    private void OpenSpeed()
    {
        var menu=new ContextMenu{PlacementTarget=speed,Placement=PlacementMode.Top};
        menu.Opened+=(_,_)=>openMenus++;menu.Closed+=(_,_)=>openMenus=Math.Max(0,openMenus-1);
        for(int n=1;n<=4;n++){int choice=n;var item=new MenuItem{Header=$"{n}×",IsCheckable=true,IsChecked=coordinator.PreferredSpeed==n};item.Click+=(_,_)=>coordinator.ChooseSpeed(choice);menu.Items.Add(item);}menu.IsOpen=true;
    }
    public void Update(EngineState? state,bool foreground)
    {
        bool usable=state is {Ready:true,Fresh:true,SceneReady:true,UiFlags:0}&&foreground;
        foreach(var cell in cells)cell.Button.IsEnabled=cell.Capability==0||usable&&(state!.Capabilities&cell.Capability)!=0;
        drink.IsEnabled=torch.IsEnabled=usable;
        bool current=state is {Ready:true,Fresh:true,SceneReady:true};
        bool keysWanted=coordinator.Preferences.AutoWalkKeys;
        bool keysActive=keysWanted&&state is {Ready:true,Fresh:true,SceneReady:true,WalkKeysEnabled:true}&&foreground&&(state.UiFlags&~8u)==0;
        walkKeys.Content=keysWanted?(keysActive?"方向键自动移动：开":"方向键自动移动：待命"):"方向键自动移动：关";
        walkKeys.Foreground=keysActive?Active:Muted;walkKeys.BorderBrush=keysWanted?Active:Muted;
        walkKeys.ToolTip="开启后轻按 ↑ ↓ ← →，沿人物所在列或行走到地图边缘后停下。\n再次按方向键停步；长按不重复。仅游戏前台生效，面板内保留原按键操作。\n关闭开关会停止本模式的行走。";
        double[] readings=[state?.Hunger??double.NaN,state?.Thirst??double.NaN,state?.Pain??double.NaN,state?.Intoxication??double.NaN];
        for(int i=0;i<4;i++){bool valid=current&&(state!.VitalValid&(1u<<i))!=0&&double.IsFinite(readings[i]);values[i].Text=valid?$"{readings[i]:0}%":"—";}
        cells[0].Button.ToolTip=!current?"等待进入游戏":state!.VisorState<0?"当前头盔没有可开合面甲":state.VisorState==1?"面甲已打开 · 点击关闭":"面甲已关闭 · 点击打开";
        cells[1].Label.Text=current?$"水 {state!.WaterUses}":"喝水";cells[2].Label.Text=current?(state!.TorchState==1?"已点亮":$"火把 {state.TorchCount}"):"火把";
        drink.ToolTip=$"{(current?$"饮水剩余 {state!.WaterUses} 次":"等待进入游戏")}\n自动喝水：{(coordinator.Preferences.AutoDrink?"开启":"关闭")}，右键切换";
        torch.ToolTip=$"{(current?$"可用火把 {state!.TorchCount} 个":"等待进入游戏")}\n自动保持点亮：{(coordinator.Preferences.AutoTorch?"开启":"关闭")}，右键切换";
        drink.BorderBrush=coordinator.Preferences.AutoDrink?Active:Muted;torch.BorderBrush=coordinator.Preferences.AutoTorch?Active:Muted;
        labels.BorderBrush=coordinator.Preferences.ShowLabels?Active:Muted;
        labels.ToolTip=coordinator.Preferences.ShowLabels?(current&&state!.HighlightApplied?"物品常显已开启":"物品常显已记住，等待游戏恢复"):"开启物品常显 · Ctrl+Alt+L";
        cells[6].Label.Visibility=Visibility.Visible;cells[6].Label.Text=$"{coordinator.PreferredSpeed}×";
        speed.Foreground=current&&state!.SuspendReasons==0&&Math.Abs(state.Multiplier-coordinator.PreferredSpeed)<.01?Active:Muted;
        cells[7].Button.IsEnabled=!coordinator.Saves.Busy;
        walking=current&&state!.WalkState==1;
        foreach(var (button,direction) in navigation){
            button.IsEnabled=state is not null&&HudPolicy.CanWalk(state.Ready,state.Fresh,state.SceneReady,foreground,state.UiFlags)&&(state.Capabilities&64)!=0;
            button.BorderBrush=walking&&(state!.WalkDirection==direction||state.WalkDirection==direction+10)?Active:Muted;
            button.ToolTip=walking?state!.WalkStatus+"\n点击任一方向停止 · Ctrl+Alt+End":direction==5?"角色走到地图中心 · Ctrl+Alt+Home":direction>=6?$"走到地图{EngineState.WalkDestinationName(direction)}后停下 · 遇敌停止":$"走到{EngineState.WalkDestinationName(direction)}并切入相邻地图 · 遇敌停止";
        }
        // Keep backup progress/results visible even after a completed journey.
        footer.Text=coordinator.Saves.Busy?coordinator.SaveStatus:walking?state!.WalkStatus:!current||coordinator.SaveStatus!="备份保存已落盘的进度"?coordinator.SaveStatus:state!.WalkState>1?state.WalkStatus:coordinator.Preferences.AutoDrink||coordinator.Preferences.AutoTorch?coordinator.SupplyStatus:coordinator.SaveStatus;
        footer.ToolTip=footer.Text;
    }
    public void Place(Native.Rect bounds,nint gameWindow)
    {
        if(handle==0)return;
        dpi=Math.Max(96,Native.GetDpiForWindow(gameWindow))/96.0;originX=bounds.Left;originY=bounds.Top;
        viewportWidth=Math.Max(1,(bounds.Right-bounds.Left)/dpi);viewportHeight=Math.Max(1,(bounds.Bottom-bounds.Top)/dpi);
        if(dragging)return;
        var p=coordinator.Preferences;
        placement=HudGeometry.Clamp(p.HasHudPlacement?new(p.HudX,p.HudY,p.HudWidth,p.HudHeight):new(16,viewportHeight*.8-HudGeometry.DefaultHeight,HudGeometry.DefaultWidth,HudGeometry.DefaultHeight),viewportWidth,viewportHeight);
        ApplyPlacement();
    }
    public void PlaceDesktop()
    {
        if(handle==0||dragging)return;
        var area=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        Place(new Native.Rect{Left=area.Left,Top=area.Top,Right=area.Right,Bottom=area.Bottom},handle);
    }
    private void ApplyPlacement()
    {
        if(handle==0)return;
        Native.SetWindowPos(handle,(nint)(-1),originX+(int)Math.Round(placement.X*dpi),originY+(int)Math.Round(placement.Y*dpi),(int)Math.Round(placement.Width*dpi),(int)Math.Round(placement.Height*dpi),0x10);
    }
    private void SavePlacement()
    {
        var p=coordinator.Preferences;p.HasHudPlacement=true;p.HudX=placement.X;p.HudY=placement.Y;p.HudWidth=placement.Width;p.HudHeight=placement.Height;coordinator.SavePreferences();
    }
}
