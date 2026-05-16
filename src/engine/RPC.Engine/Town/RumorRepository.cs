using System.Text.Json;
using RPC.Engine.Combat;
using RPC.Engine.Content;

namespace RPC.Engine.Town;

public class RumorRepository
{
    private readonly List<RumorDef> _rumors;

    public RumorRepository(List<RumorDef> rumors)
    {
        _rumors = rumors ?? new List<RumorDef>();
    }

    public RumorRepository(IContentCatalog catalog)
    {
        _rumors = new List<RumorDef>();
        foreach (var file in catalog.EnumerateFiles("rumors", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"rumors/{Path.GetFileName(file)}");
            if (json == null) continue;
            var defs = JsonSerializer.Deserialize<List<RumorDef>>(json, ContentJsonOptions.Standard);
            if (defs != null)
                _rumors.AddRange(defs);
        }
    }

    public IReadOnlyList<RumorDef> Rumors => _rumors;

    public List<TownRumor> GenerateForVisit(GameRandom rng, int count)
    {
        if (_rumors.Count == 0)
            return new List<TownRumor>();

        count = Math.Clamp(count, 3, 5);
        var shuffled = _rumors.OrderBy(_ => rng.Next(int.MaxValue)).ToList();
        var selected = shuffled.Take(Math.Min(count, shuffled.Count));

        return selected.Select(r => new TownRumor(
            r.Id,
            r.Text,
            r.TruthStatus,
            false,
            null,
            r.RelatedContentId,
            r.RelatedFactionId)).ToList();
    }

    public bool VerifyRumor(TownRumor rumor, RumorVerificationSource source, GameRandom rng)
    {
        var accuracy = source switch
        {
            RumorVerificationSource.AshmouthBroker => 1.0,
            RumorVerificationSource.Firsthand => 1.0,
            RumorVerificationSource.InkbloodScribe => 0.8,
            RumorVerificationSource.HollowContact => 0.8,
            _ => 0.5
        };

        var roll = rng.Roll(1, 100) / 100.0;
        if (roll > accuracy)
            return rng.Next(2) == 0; // Random result on failed verification

        return rumor.TruthStatus == RumorTruthStatus.True;
    }
}
