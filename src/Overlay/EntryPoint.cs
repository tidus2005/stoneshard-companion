namespace StoneshardCompanion;

public static class EntryPoint
{
    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--save-worker")return SaveWorker.Run(args);
        if(args.Length==2&&args[0]=="--apply-update")return AppUpdater.Apply(args[1]);
        try{var app=new App();app.InitializeComponent();return app.Run();}
        catch(Exception e){
            Console.Error.WriteLine(e);
            UserPreferences.Log("startup-failed "+e);
            System.Windows.MessageBox.Show("助手启动失败："+e.Message+"\n请查看启动日志或将此信息反馈。","晶石助手",System.Windows.MessageBoxButton.OK,System.Windows.MessageBoxImage.Error);
            return 1;
        }
    }
}
