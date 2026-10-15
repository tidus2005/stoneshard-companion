using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StoneshardCompanion;

// Player-authored notes only: no hidden inventory, crafting or save commands.
public sealed class JourneyChecklistWindow : Window
{
    public JourneyChecklistWindow(MainWindow owner)
    {
        Title="出发清单 · 本次行旅";Width=510;Height=690;MinWidth=380;MinHeight=400;Topmost=true;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=new SolidColorBrush(Color.FromRgb(25,23,31));Foreground=Brushes.Wheat;FontFamily=new FontFamily("Microsoft YaHei UI");
        var panel=new StackPanel{Margin=new Thickness(22)};
        Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(new TextBlock{Text="出发前，再看一眼",FontSize=23,Margin=new Thickness(0,0,0,12)});
        panel.Children.Add(new TextBlock{Text="按自己的行程逐项确认。勾选仅用于本窗口，重新打开会清空；清单文字会保存。不会自动补货、制作、存档或判断旅途安全。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,0,0,15)});
        var items=new StackPanel();panel.Children.Add(items);
        void Rebuild(){
            items.Children.Clear();
            foreach(string line in owner.Preferences.JourneyChecklist.Split('\n').Select(s=>s.Trim()).Where(s=>s.Length>0).Take(40))
                items.Children.Add(new CheckBox{Content=new TextBlock{Text=line,TextWrapping=TextWrapping.Wrap},Foreground=Brushes.Wheat,Margin=new Thickness(0,7,0,7)});
        }
        Rebuild();
        var reset=new Button{Content="重新检查本次行旅",Margin=new Thickness(0,15,0,10)};reset.Click+=(_,_)=>Rebuild();panel.Children.Add(reset);
        panel.Children.Add(new TextBlock{Text="自定义通用清单（每行一项，最多 40 项）",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,7)});
        var editor=new TextBox{Text=owner.Preferences.JourneyChecklist,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=175,MaxLength=4000};panel.Children.Add(editor);
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,8,0,0)};
        var save=new Button{Content="保存清单文字并重新检查",Margin=new Thickness(0,10,0,0)};
        save.Click+=(_,_)=>{
            string[] lines=editor.Text.Split('\n').Select(s=>s.Trim()).Where(s=>s.Length>0).ToArray();
            if(lines.Length>40){status.Text="最多保留 40 项，请合并或删减后保存。";return;}
            string old=owner.Preferences.JourneyChecklist;
            owner.Preferences.JourneyChecklist=string.Join('\n',lines);
            try{owner.Preferences.Save();editor.Text=owner.Preferences.JourneyChecklist;Rebuild();status.Text="清单文字已保存，本次勾选已清空。";}
            catch(Exception e){owner.Preferences.JourneyChecklist=old;status.Text="保存失败："+e.Message;}
        };panel.Children.Add(save);panel.Children.Add(status);
    }
}
