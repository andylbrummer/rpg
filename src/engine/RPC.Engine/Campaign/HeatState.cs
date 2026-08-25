namespace RPC.Engine.Campaign;

public enum HeatTier
{
    None,
    PricePenalty,
    Patrols,
    ContactsRefuse,
    Lockdown
}

public class HeatState
{
    private int _value = 0;

    public int Value
    {
        get => _value;
        set => _value = Math.Clamp(value, 0, 100);
    }

    public HeatTier Tier => Value switch
    {
        <= 20 => HeatTier.None,
        <= 40 => HeatTier.PricePenalty,
        <= 60 => HeatTier.Patrols,
        <= 80 => HeatTier.ContactsRefuse,
        _ => HeatTier.Lockdown
    };

    public void Add(int amount) => Value += amount;

    public bool HasPricePenalty => Tier >= HeatTier.PricePenalty;
    public bool HasPatrols => Tier >= HeatTier.Patrols;
    public bool ContactsRefuse => Tier >= HeatTier.ContactsRefuse;
    public bool IsLockdown => Tier >= HeatTier.Lockdown;
}
