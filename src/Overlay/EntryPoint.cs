namespace StoneshardCompanion;

public static class EntryPoint
{
    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--save-worker")return SaveWorker.Run(args);
        var app=new App();app.InitializeComponent();return app.Run();
    }
}
