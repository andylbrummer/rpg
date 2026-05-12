using RPC.Engine.Character;

namespace RPC.Engine.Campaign;

public enum EvidenceChannel
{
    DocumentaryContradictions,
    NpcBehavioralShifts,
    OperationalAnomalies,
    FactionPatternRecognition,
    DirectConfrontation
}

public record EvidenceAdded(string FactionId, int Amount, int NewValue, string Source, int ThresholdReached);

public class EvidenceState
{
    private readonly Dictionary<string, int> _counters = new();
    private readonly Dictionary<string, Dictionary<EvidenceChannel, int>> _channelCounters = new();
    private string? _suspectedFaction;

    public IReadOnlyDictionary<string, int> Counters => _counters;

    public string? SuspectedFaction => _suspectedFaction;

    public EvidenceAdded AddEvidence(string factionId, string source, int amount = 1)
    {
        var oldValue = _counters.TryGetValue(factionId, out var v) ? v : 0;
        var newValue = oldValue + amount;
        _counters[factionId] = newValue;

        var newThreshold = GetThresholdLevel(newValue);

        if (_suspectedFaction == null && newThreshold >= 3)
        {
            _suspectedFaction = factionId;
        }

        return new EvidenceAdded(factionId, amount, newValue, source, newThreshold);
    }

    public EvidenceAdded AddEvidence(string factionId, EvidenceChannel channel, string source)
    {
        if (!_channelCounters.TryGetValue(factionId, out var channels))
        {
            channels = new Dictionary<EvidenceChannel, int>();
            _channelCounters[factionId] = channels;
        }
        channels[channel] = channels.TryGetValue(channel, out var cv) ? cv + 1 : 1;

        return AddEvidence(factionId, source, 1);
    }

    public int GetChannelCount(string factionId, EvidenceChannel channel)
    {
        if (_channelCounters.TryGetValue(factionId, out var channels))
        {
            return channels.TryGetValue(channel, out var v) ? v : 0;
        }
        return 0;
    }

    public int GetThreshold(string factionId)
    {
        var value = _counters.TryGetValue(factionId, out var v) ? v : 0;
        return GetThresholdLevel(value);
    }

    public bool HasReachedThreshold(string factionId, int threshold)
    {
        return GetThreshold(factionId) >= threshold;
    }

    public bool CanClassReveal(int threshold, string classId, string? branchId)
    {
        return (classId == "inkblood" && branchId == "archivist" && threshold >= 3)
            || (classId == "ashmouth" && branchId == "broker" && threshold >= 5);
    }

    public string? GetRevelationForParty(CharacterState[] party)
    {
        var maxThreshold = _counters.Count > 0 ? _counters.Values.Max(GetThresholdLevel) : 0;
        bool hasArchivist = false;
        bool hasBroker = false;
        foreach (var member in party)
        {
            if (member.Id == Guid.Empty) continue;
            if (CanClassReveal(maxThreshold, member.ClassId, member.BranchChoice))
            {
                if (member.ClassId == "inkblood") hasArchivist = true;
                if (member.ClassId == "ashmouth") hasBroker = true;
            }
        }
        if (hasBroker) return "broker_insight";
        if (hasArchivist) return "archivist_insight";
        return null;
    }

    public void SetCounter(string factionId, int value)
    {
        _counters[factionId] = value;
        if (_suspectedFaction == null && GetThresholdLevel(value) >= 3)
        {
            _suspectedFaction = factionId;
        }
    }

    public void SetSuspectedFaction(string? factionId)
    {
        _suspectedFaction = factionId;
    }

    public void Clear()
    {
        _counters.Clear();
        _channelCounters.Clear();
        _suspectedFaction = null;
    }

    private static int GetThresholdLevel(int value)
    {
        if (value >= 10) return 10;
        if (value >= 7) return 7;
        if (value >= 5) return 5;
        if (value >= 3) return 3;
        return 0;
    }
}
