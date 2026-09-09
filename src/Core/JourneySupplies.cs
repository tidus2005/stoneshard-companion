namespace StoneshardCompanion;

public sealed record Provision(string Name,double Uses,double? Hunger,double? Thirst,double? FreshHours,bool Water);
public sealed record EquipmentWear(string Name,double Current,double Maximum);
public sealed class JourneyTelemetry
{
    public double? Minutes {get;set;}
    public Provision[] Provisions {get;set;}=[];
    public EquipmentWear[] Equipment {get;set;}=[];
    public bool Complete {get;set;}
}
public sealed record SupplyEstimate(double? FoodHours,double? WaterHours,double? HungerPerHour,double? ThirstPerHour,string Explanation);

// Measures game-time needs rather than wall-clock time or the selected speed.
public sealed class JourneyEstimator
{
    private double? player,minute,lastHunger,lastThirst;
    private double hungerGain,thirstGain,hungerMinutes,thirstMinutes;
    private long lastAt;
    public void Reset(){player=minute=lastHunger=lastThirst=null;hungerGain=thirstGain=hungerMinutes=thirstMinutes=0;lastAt=0;}
    public SupplyEstimate Update(CharacterTelemetry data,double hunger,double thirst,double foodRate=0,double waterRate=0)
    {
        var trip=data.Journey;
        if(!data.Fresh(Environment.TickCount64)||trip.Minutes is not double now||!double.IsFinite(now)||!double.IsFinite(hunger)||!double.IsFinite(thirst))return new(null,null,null,null,"等待新鲜的背包与游戏时间数据。");
        double sampledHunger=data.Stats.FirstOrDefault(s=>s.Key=="Hunger")?.Value??hunger;
        double sampledThirst=data.Stats.FirstOrDefault(s=>s.Key=="Thirsty")?.Value??thirst;
        if(minute>now||lastAt>data.At)Reset();
        // A new map creates a new player instance. Preserve learned rates,
        // but never treat travel/loading time as an ordinary movement sample.
        if(player==data.Player&&data.At!=lastAt&&minute is double previous&&now>previous){
            double elapsed=now-previous;
            // Ignore consumption, large jumps, loading and sleeps; they mix rates.
            if(elapsed<=30){
                double dh=sampledHunger-lastHunger!.Value,dt=sampledThirst-lastThirst!.Value;
                if(dh>=0&&dh/elapsed<=2){hungerGain+=dh;hungerMinutes+=elapsed;}
                if(dt>=0&&dt/elapsed<=2){thirstGain+=dt;thirstMinutes+=elapsed;}
                if(hungerMinutes>120){hungerGain*=.5;hungerMinutes*=.5;}
                if(thirstMinutes>120){thirstGain*=.5;thirstMinutes*=.5;}
            }
        }
        // UI polls faster than telemetry. Keep the previous whole sample until
        // a new timestamp arrives, rather than replacing needs with later frames.
        if(data.At!=lastAt){
            if(minute is null||now>minute||player!=data.Player){player=data.Player;minute=now;lastHunger=sampledHunger;lastThirst=sampledThirst;}
            lastAt=data.At;
        }
        double? hr=foodRate>0?foodRate:hungerMinutes>=6&&hungerGain>0?hungerGain/hungerMinutes*60:null;
        double? tr=waterRate>0?waterRate:thirstMinutes>=6&&thirstGain>0?thirstGain/thirstMinutes*60:null;
        double? fh=trip.Complete?Hours(hunger,hr,trip.Provisions.Where(p=>!p.Water),false):null;
        double? wh=trip.Complete?Hours(thirst,tr,trip.Provisions.Where(p=>p.Water),true):null;
        string rates=$"饥饿 {Rate(hr)}；口渴 {Rate(tr)}。";
        string detail=$"以饥饿／口渴 25% 为补给界限，单位为游戏内小时。\n{rates}\n"+
            (foodRate>0||waterRate>0?"使用设置中的消耗率；未手动设置的项目仍按实测估算。":"按最近行走／行动期间的实际增长估算，累计至少 6 分钟游戏时间后显示。")+
            "\n每次在 25% 补给，超过 25 点的单次回复按 25 点计入；食物按剩余保鲜时间排序，过期后不计入。水只计饮水容器，未把药酒或食物汁水计入。天气、战斗、负面状态、盐分及食物效果变化会改变实际消耗。"+
            (!trip.Complete?"\n背包数据不完整，暂不显示续航。":"")+
            "\n未读取到回复量的食物不计入估算（以 — 标示）。\n\n随身补给：\n"+string.Join("\n",trip.Provisions.Select(p=>$"• {StatMechanics.CleanLabel(p.Name)} ×{p.Uses:0} · 饥饿 {CharacterTelemetry.Format(p.Hunger,"")} / 口渴 {CharacterTelemetry.Format(p.Thirst,"")}"));
        return new(fh,wh,hr,tr,detail);
    }
    private static string Rate(double? value)=>value is null?"消耗率采集中":$"{value:0.##} 个百分点／小时";
    public static double? Hours(double need,double? rate,IEnumerable<Provision> items,bool water)
    {
        if(rate is not >0||!double.IsFinite(rate.Value)||!double.IsFinite(need))return null;
        double hours=(25-Math.Clamp(need,0,100))/rate.Value;
        foreach(var item in items.OrderBy(p=>p.FreshHours??double.PositiveInfinity)){
            double? delta=water?item.Thirst:item.Hunger;
            if(delta is not <0||!double.IsFinite(delta.Value)||!double.IsFinite(item.Uses))continue;
            double per=Math.Min(25,-delta.Value)/rate.Value;
            double uses=Math.Clamp(Math.Floor(item.Uses),0,10000);
            if(item.FreshHours is double expiry){
                if(expiry<=Math.Max(0,hours))continue;
                uses=Math.Min(uses,Math.Ceiling((expiry-hours)/per));
            }
            hours+=per*uses;
        }
        return Math.Max(0,hours);
    }
    public static int Severity(EquipmentWear wear,double warning)=>wear.Maximum<=0?0:wear.Current<=0?2:wear.Current/wear.Maximum*100<=warning?1:0;
    public static string Duration(double? hours)=>hours is null?"采集中":hours<24?$"约 {hours:0.0} 小时":$"约 {hours/24:0.0} 天";
}
