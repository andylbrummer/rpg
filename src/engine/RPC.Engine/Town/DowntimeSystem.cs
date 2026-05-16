using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Inventory;
using RPC.Engine.Party;
using RPC.Engine.Combat;

namespace RPC.Engine.Town;

public enum DowntimeAction
{
    Rest,
    Train,
    Craft,
    Network,
    Investigate,
    LayLow,
    TendBlooms
}

public record DowntimeResult(
    bool Success,
    string ActionType,
    string Message,
    int? HpRestored = null,
    int? XpGained = null,
    string? ItemId = null,
    int? ItemCount = null,
    string? FactionId = null,
    int? RepDelta = null,
    string? EvidenceFaction = null,
    int? HeatDelta = null);

public static class DowntimeSystem
{
    private static readonly string[] CraftableComponents = new[]
    {
        "bone_fragment",
        "cautery_supply",
        "ink_vial",
        "engine_charge"
    };

    private static readonly string[] KnownFactions = new[]
    {
        "bureau",
        "convocation",
        "stillness",
        "inkblood",
        "cartography"
    };

    public static DowntimeResult PerformAction(
        CharacterState character,
        DowntimeAction action,
        PartyState party,
        ReputationState reputation,
        EvidenceState evidence,
        GameRandom rng)
    {
        return action switch
        {
            DowntimeAction.Rest => PerformRest(character, party),
            DowntimeAction.Train => PerformTrain(character, party),
            DowntimeAction.Craft => PerformCraft(character, party, rng),
            DowntimeAction.Network => PerformNetwork(reputation, rng),
            DowntimeAction.Investigate => PerformInvestigate(evidence, rng),
            DowntimeAction.LayLow => PerformLayLow(reputation),
            DowntimeAction.TendBlooms => PerformTendBlooms(character, party, rng),
            _ => new DowntimeResult(false, action.ToString(), "Unknown downtime action.")
        };
    }

    private static DowntimeResult PerformRest(CharacterState character, PartyState party)
    {
        var maxHp = character.GetEffectiveStats().MaxHp;
        var hpRestored = maxHp - character.CurrentHp;

        if (hpRestored <= 0 && (character.TempModifiers == null || character.TempModifiers.Length == 0))
        {
            return new DowntimeResult(true, "Rest", "Already fully rested.", HpRestored: 0);
        }

        var index = Array.IndexOf(party.Members, character);
        if (index < 0)
            return new DowntimeResult(false, "Rest", "Character not found in party.");

        var updated = character with
        {
            CurrentHp = maxHp,
            TempModifiers = Array.Empty<TempStatModifier>()
        };
        party.SetMember(index, updated);

        return new DowntimeResult(true, "Rest", $"Rested and recovered {hpRestored} HP.", HpRestored: hpRestored);
    }

    private static DowntimeResult PerformTrain(CharacterState character, PartyState party)
    {
        var index = Array.IndexOf(party.Members, character);
        if (index < 0)
            return new DowntimeResult(false, "Train", "Character not found in party.");

        const int xpGain = 15;
        var updated = character with { Xp = character.Xp + xpGain };
        party.SetMember(index, updated);

        return new DowntimeResult(true, "Train", $"Trained and gained {xpGain} XP.", XpGained: xpGain);
    }

    private static DowntimeResult PerformCraft(CharacterState character, PartyState party, GameRandom rng)
    {
        var index = Array.IndexOf(party.Members, character);
        if (index < 0)
            return new DowntimeResult(false, "Craft", "Character not found in party.");

        var itemId = CraftableComponents[rng.Roll(0, CraftableComponents.Length - 1)];
        const int count = 1;

        if (!ComponentInventorySystem.CanAddComponent(character.ComponentInventory, itemId, count, maxSlots: 8))
        {
            return new DowntimeResult(false, "Craft", "No space in component inventory.");
        }

        var newComponents = ComponentInventorySystem.AddComponent(character.ComponentInventory, itemId, count, maxSlots: 8);
        var updated = character with { ComponentInventory = newComponents };
        party.SetMember(index, updated);

        return new DowntimeResult(true, "Craft", $"Crafted {itemId}.", ItemId: itemId, ItemCount: count);
    }

    private static DowntimeResult PerformNetwork(ReputationState reputation, GameRandom rng)
    {
        var factionId = KnownFactions[rng.Roll(0, KnownFactions.Length - 1)];
        reputation.ApplyDelta(factionId, 5, "downtime_network");

        return new DowntimeResult(true, "Network", $"Networked with {factionId}. Reputation +5.", FactionId: factionId, RepDelta: 5);
    }

    private static DowntimeResult PerformInvestigate(EvidenceState evidence, GameRandom rng)
    {
        var factionId = KnownFactions[rng.Roll(0, KnownFactions.Length - 1)];
        evidence.AddEvidence(factionId, "downtime_investigate");
        var newValue = evidence.Counters.TryGetValue(factionId, out var v) ? v : 0;

        return new DowntimeResult(true, "Investigate", $"Investigated {factionId}. Evidence accumulated.", EvidenceFaction: factionId);
    }

    private static DowntimeResult PerformLayLow(ReputationState reputation)
    {
        var mostNegative = KnownFactions
            .Select(f => (FactionId: f, Rep: reputation[f]))
            .Where(x => x.Rep < 0)
            .OrderBy(x => x.Rep)
            .FirstOrDefault();

        if (mostNegative.FactionId == null)
        {
            return new DowntimeResult(true, "LayLow", "No negative reputation to mitigate.", RepDelta: 0, HeatDelta: -30);
        }

        var oldRep = reputation[mostNegative.FactionId];
        var targetRep = Math.Min(0, oldRep + 3);
        var actualDelta = targetRep - oldRep;
        reputation.ApplyDelta(mostNegative.FactionId, actualDelta, "downtime_laylow");

        return new DowntimeResult(true, "LayLow", $"Laid low. {mostNegative.FactionId} reputation improved by {actualDelta}.", FactionId: mostNegative.FactionId, RepDelta: actualDelta, HeatDelta: -30);
    }

    private static DowntimeResult PerformTendBlooms(CharacterState character, PartyState party, GameRandom rng)
    {
        var index = Array.IndexOf(party.Members, character);
        if (index < 0)
            return new DowntimeResult(false, "TendBlooms", "Character not found in party.");

        var maxHp = character.GetEffectiveStats().MaxHp;
        var healAmount = Math.Max(1, maxHp / 4);
        var newHp = Math.Min(maxHp, character.CurrentHp + healAmount);
        var actualHeal = newHp - character.CurrentHp;

        // Attempt to grant a bloom essence component
        const string bloomItemId = "bloom_essence";
        const int bloomCount = 1;
        string? grantedItem = null;

        if (ComponentInventorySystem.CanAddComponent(character.ComponentInventory, bloomItemId, bloomCount, maxSlots: 8))
        {
            var newComponents = ComponentInventorySystem.AddComponent(character.ComponentInventory, bloomItemId, bloomCount, maxSlots: 8);
            var updated = character with
            {
                CurrentHp = newHp,
                ComponentInventory = newComponents
            };
            party.SetMember(index, updated);
            grantedItem = bloomItemId;
        }
        else
        {
            var updated = character with { CurrentHp = newHp };
            party.SetMember(index, updated);
        }

        var message = grantedItem != null
            ? $"Tended blooms. Healed {actualHeal} HP and gathered {bloomItemId}."
            : $"Tended blooms. Healed {actualHeal} HP.";

        return new DowntimeResult(true, "TendBlooms", message, HpRestored: actualHeal, ItemId: grantedItem, ItemCount: grantedItem != null ? 1 : null);
    }
}
