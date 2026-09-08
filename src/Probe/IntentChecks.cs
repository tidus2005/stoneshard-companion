using StoneshardCompanion;

internal static class IntentChecks
{
    public static void Run()
    {
        int passed=0;var intent=new SpeedIntent();
        void Check(bool condition,string name){if(!condition)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
        intent.BeginSession("game-a",false,4);Check(intent.Multiplier==1,"new process starts normally");
        foreach(int speed in new[]{2,3,4}){
            intent.Select(speed);long revision=intent.Revision;
            for(int n=0;n<10;n++){intent.BeginSession("game-a",false,1);Check(intent.Multiplier==speed&&intent.Revision==revision,"room/window reconnect preserves selection");}
            Check(intent.NeedsApply(1)&&intent.Multiplier==speed,"background or watchdog observation cannot replace selection");
        }
        long previous=intent.Revision;intent.Select(2);Check(intent.Revision>previous&&intent.Multiplier==2,"new selection supersedes previous revision");
        intent.Stop();intent.BeginSession("game-a",true,4);Check(intent.Multiplier==1&&intent.NeedsApply(4),"explicit stop cancels old fast selection");
        intent.BeginSession("game-b",false,4);Check(intent.Multiplier==1,"new game defaults to normal");
        intent.BeginSession("game-c",true,3);Check(intent.Multiplier==3,"optional next-launch preference");
        try{intent.Select(0);throw new Exception("accepted invalid speed");}catch(ArgumentOutOfRangeException){passed++;}
        Console.WriteLine($"{passed} intent checks passed.");
    }
}
