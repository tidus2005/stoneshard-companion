namespace StoneshardCompanion;

public readonly record struct SupplyObservation(long Now,ulong Scene,bool Ready,bool Foreground,bool Safe,bool CanAct,double Thirst,int WaterUses,int TorchState,int TorchCount,bool NeedsLight);
public enum SupplyAction { None,Drink,TorchOn,TorchOff }

/// <summary>Bounded automation: one action at a time, hysteresis, cooldown and failure latch.</summary>
public sealed class SupplyPolicy
{
    private bool drinkArmed=true,failed,awaiting;
    private long nextAttempt;
    private ulong scene;
    private bool lastDrink,lastTorch;
    private double threshold;
    private double thirstBefore=double.NaN;
    private int waterBefore;
    public string Status {get;private set;}="自动补给已关闭";
    public void Reset(){drinkArmed=true;failed=false;awaiting=false;nextAttempt=0;scene=0;thirstBefore=double.NaN;Status="自动补给已关闭";}
    public SupplyAction Evaluate(SupplyObservation s,bool autoDrink,bool autoTorch,double drinkThreshold)
    {
        drinkThreshold=Math.Clamp(double.IsFinite(drinkThreshold)?drinkThreshold:25,10,80);
        if(lastDrink!=autoDrink||lastTorch!=autoTorch||threshold!=drinkThreshold){failed=false;drinkArmed=true;lastDrink=autoDrink;lastTorch=autoTorch;threshold=drinkThreshold;}
        if(scene!=s.Scene){scene=s.Scene;nextAttempt=Math.Max(nextAttempt,s.Now+2000);}
        if(!autoDrink&&!autoTorch){Status="自动补给已关闭";return SupplyAction.None;}
        if(failed){Status="自动补给已暂停：操作未确认；重新开启后重试";return SupplyAction.None;}
        if(!s.Ready||!s.Foreground||!s.Safe||!s.CanAct){Status="自动补给等待安全空闲状态";return SupplyAction.None;}
        if(awaiting||s.Now<nextAttempt)return SupplyAction.None;
        var waiting=new List<string>();
        if(autoDrink)waiting.Add(!double.IsFinite(s.Thirst)?"口渴读取中":s.WaterUses==0?"无可用饮水":$"饮水阈值 {drinkThreshold:0}%");
        if(autoTorch)waiting.Add(s.TorchCount==0?"无可用火把":s.TorchState==1?"火把已点亮":"火把待点亮");
        Status=string.Join(" · ",waiting);
        if(double.IsFinite(s.Thirst)&&s.Thirst<=drinkThreshold-5)drinkArmed=true;
        // Confirm both relief and a consumed charge before allowing another drink
        // while still above the threshold. A stale snapshot must never loop.
        if(!drinkArmed&&double.IsFinite(s.Thirst)&&s.Thirst<thirstBefore&&s.WaterUses<waterBefore)drinkArmed=true;
        SupplyAction action=SupplyAction.None;
        if(autoDrink&&drinkArmed&&double.IsFinite(s.Thirst)&&s.Thirst>=drinkThreshold){
            if(s.WaterUses>0)action=SupplyAction.Drink;else Status="自动喝水：没有可用饮水";
        }
        if(action==SupplyAction.None&&autoTorch){
            if(s.NeedsLight&&s.TorchState==0&&s.TorchCount>0)action=SupplyAction.TorchOn;
            else if(!s.NeedsLight&&s.TorchState==1)action=SupplyAction.TorchOff;
        }
        if(action!=SupplyAction.None){
            if(action==SupplyAction.Drink){thirstBefore=s.Thirst;waterBefore=s.WaterUses;}
            awaiting=true;nextAttempt=s.Now+3000;Status="正在执行自动补给";
        }
        return action;
    }
    public void Complete(SupplyAction action,bool acknowledged,long now)
    {
        awaiting=false;failed=!acknowledged;nextAttempt=now+3000;
        if(action==SupplyAction.Drink&&acknowledged)drinkArmed=false;
        Status=acknowledged?"自动补给已确认":"自动补给已暂停：未重复执行";
    }
    public void ManualAction(long now){nextAttempt=now+3000;}
}
