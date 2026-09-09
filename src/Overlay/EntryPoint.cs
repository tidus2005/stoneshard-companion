namespace StoneshardCompanion;

public static class EntryPoint
{
    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--save-worker")return SaveWorker.Run(args);
        if(args.Length==2&&args[0]=="--apply-update")return AppUpdater.Apply(args[1]);
        var app=new App();app.InitializeComponent();return app.Run();
    }
}
