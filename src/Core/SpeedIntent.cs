namespace StoneshardCompanion;

/// <summary>Player intent outlives rooms, windows and transport connections.</summary>
public sealed class SpeedIntent
{
    public int Multiplier { get; private set; } = 1;
    public long Revision { get; private set; }
    public string? SessionKey { get; private set; }
    public void BeginSession(string key, bool remember, int saved)
    {
        if (SessionKey == key) return;
        SessionKey = key;
        Select(remember && saved is >=1 and <=4 ? saved : 1);
    }
    public void Select(int multiplier)
    {
        if (multiplier is <1 or >4) throw new ArgumentOutOfRangeException(nameof(multiplier));
        Multiplier = multiplier;
        Revision++;
    }
    public void Stop() => Select(1);
    public bool NeedsApply(double enginePreference) => Math.Abs(enginePreference-Multiplier)>.01;
}
