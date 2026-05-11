using System.Collections;

namespace RPC.Engine;

public record ReputationChange(string FactionId, int Delta, int NewValue, string Source);

public class ReputationState : IEnumerable<KeyValuePair<string, int>>
{
    private readonly Dictionary<string, int> _values = new();
    private readonly Dictionary<string, string> _opposedPairs;
    private readonly double _propagationRate;

    public ReputationState(double propagationRate = 0.4)
    {
        _propagationRate = propagationRate;
        _opposedPairs = new Dictionary<string, string>
        {
            ["bureau"] = "convocation",
            ["convocation"] = "bureau",
            ["fieldwright"] = "inkblood",
            ["inkblood"] = "fieldwright"
        };
    }

    public int this[string factionId]
    {
        get => _values.TryGetValue(factionId, out var v) ? v : 0;
        set => _values[factionId] = Math.Clamp(value, -100, 100);
    }

    public IReadOnlyList<ReputationChange> ApplyDelta(string factionId, int delta, string source)
    {
        var changes = new List<ReputationChange>();
        var clampedDelta = Math.Clamp(delta, -100, 100);

        var oldValue = this[factionId];
        var newValue = Math.Clamp(oldValue + clampedDelta, -100, 100);
        this[factionId] = newValue;
        changes.Add(new ReputationChange(factionId, newValue - oldValue, newValue, source));

        if (_opposedPairs.TryGetValue(factionId, out var opposedId))
        {
            var opposedDelta = (int)Math.Round(-clampedDelta * _propagationRate);
            if (opposedDelta != 0)
            {
                var oldOpposed = this[opposedId];
                var newOpposed = Math.Clamp(oldOpposed + opposedDelta, -100, 100);
                this[opposedId] = newOpposed;
                changes.Add(new ReputationChange(opposedId, newOpposed - oldOpposed, newOpposed, source));
            }
        }

        return changes;
    }

    public void Clear() => _values.Clear();

    public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
