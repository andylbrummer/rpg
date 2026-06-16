using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;
using RPC.Engine.Town;

namespace RPC.Engine.Town;

public class TownService
{
    private readonly FactionContentRepository? _factionContent;
    private readonly RumorRepository? _rumors;

    public TownService(FactionContentRepository? factionContent = null, RumorRepository? rumors = null)
    {
        _factionContent = factionContent;
        _rumors = rumors;
    }

    public List<FactionContact> GenerateContacts() => _factionContent?.GenerateContacts() ?? new List<FactionContact>();
    public List<MissionOffer> GenerateMissions() => _factionContent?.GenerateMissions() ?? new List<MissionOffer>();
    public List<FactionVendor> GenerateVendors() => _factionContent?.GenerateVendors() ?? new List<FactionVendor>();

    public List<TownRumor> GenerateRumorsForVisit(GameRandom rng, int count) => _rumors?.GenerateForVisit(rng, count) ?? new List<TownRumor>();

    public bool VerifyRumor(TownRumor rumor, RumorVerificationSource source, GameRandom rng) =>
        _rumors?.VerifyRumor(rumor, source, rng) ?? false;


    public void RestAtInn(GameState state)
    {
        if (state.Mode != GameMode.Menu) return;
        foreach (var member in state.Party.Members)
        {
            if (member.Id == Guid.Empty) continue;
            var maxHp = member.GetEffectiveStats().MaxHp;
            var index = Array.IndexOf(state.Party.Members, member);
            state.Party.SetMember(index, member with { CurrentHp = maxHp, TempModifiers = Array.Empty<TempStatModifier>() });
        }
        state.LastUpdate = DateTime.UtcNow;
    }

    public DowntimeResult? PerformDowntimeAction(GameState state, Guid characterId, DowntimeAction action)
    {
        if (state.Mode != GameMode.Menu) return null;
        if (state._downtimeCompleted.Contains(characterId)) return null;

        var character = state.Party.Members.FirstOrDefault(m => m.Id == characterId);
        if (character.Id == Guid.Empty) return null;

        var result = DowntimeSystem.PerformAction(character, action, state.Party, state.Reputation, state.Evidence, state._encounterRng);
        if (result.Success)
        {
            state._downtimeCompleted.Add(characterId);
            state.EmitActionLog("downtime", action.ToString().ToLowerInvariant(), new Dictionary<string, string>
            {
                { "characterId", characterId.ToString() },
                { "characterName", character.Name },
                { "action", action.ToString() },
                { "message", result.Message }
            });
            if (result.HeatDelta.HasValue && result.HeatDelta.Value != 0)
            {
                var oldHeat = state.Heat.Value;
                state.Heat.Add(result.HeatDelta.Value);
                state.EmitActionLog("heat", "downtime_change", new Dictionary<string, string>
                {
                    { "action", action.ToString() },
                    { "delta", result.HeatDelta.Value.ToString() },
                    { "oldValue", oldHeat.ToString() },
                    { "newValue", state.Heat.Value.ToString() }
                });
            }
        }
        return result;
    }

    public void ReturnToTown(GameState state)
    {
        if (state.CurrentDungeon != null)
        {
            state.EmitActionLog("dungeon", "dungeon_completed", new Dictionary<string, string> { { "dungeonType", state.CurrentDungeonType ?? "" } });
        }
        state.Mode = GameMode.Menu;
        state.CurrentDungeon = null;
        state.LastUpdate = DateTime.UtcNow;
        state.IncrementTurns(1);
        state._downtimeCompleted.Clear();
        state.CheckWildCardTrigger();
        RefreshExclusiveRecruits(state);
        GenerateRumors(state);
        CheckTitheOnTownEntry(state);
    }

    // --- Tithe obligations (Ossuary Compact / "inkblood" faction) -----------------------------
    // Design: docs/design/05-characters-and-classes.md "Tithe Obligations".

    /// <summary>The faction tithe is owed to (the Ossuary Compact, internally the inkblood faction).</summary>
    public const string TitheFactionId = "inkblood";

    /// <summary>Campaign-act milestones (overworld turns) at which a tithe falls due.</summary>
    public static readonly int[] TitheMilestones = { 1, 15, 25 };

    /// <summary>Ossuary Compact reputation lost per unpaid tithe token when a milestone goes unpaid.</summary>
    public const int TitheRepPenaltyPerToken = 10;

    /// <summary>Late-payment gold surcharge rate (25%) applied when paying after the milestone turn.</summary>
    public const double TitheLateGoldSurchargeRate = 0.25;

    /// <summary>
    /// Nominal gold value of a tithe token, used solely as the base for the late-payment gold
    /// surcharge. Tithe itself is paid in tokens; this constant only scales the 25% late penalty.
    /// Exposed as a named constant so the designer can tune the late-payment cost.
    /// </summary>
    public const int TitheGoldValuePerToken = 100;

    /// <summary>Tithe cost for an active party of the given size: one token per three members, rounded up.</summary>
    public static int TitheCostForPartySize(int activePartySize) =>
        (int)Math.Ceiling(activePartySize / 3.0);

    /// <summary>
    /// Bill any tithe milestones (turns 1/15/25) the party has reached but not yet been billed for.
    /// Each newly due milestone adds ceil(activePartySize/3) tokens to the outstanding debt and
    /// applies the Compact non-payment penalty (-10 reputation per unpaid token). Ongoing penalties
    /// (+50% component/vendor cost, contacts refuse) follow from <see cref="TitheState.HasDebt"/>
    /// and lift when <see cref="PayTithe"/> clears the debt.
    /// </summary>
    public void CheckTitheOnTownEntry(GameState state)
    {
        var turn = state.Overworld.Turns;
        foreach (var milestone in TitheMilestones)
        {
            if (turn < milestone) continue;
            if (state.Tithe.BilledMilestones.Contains(milestone)) continue;

            var activeSize = state.Party.Active.Count();
            state.Tithe.BilledMilestones.Add(milestone);

            var cost = TitheCostForPartySize(activeSize);
            if (cost <= 0) continue; // an empty party owes nothing

            state.Tithe.Debt += cost;
            state.Tithe.OutstandingSinceTurn ??= milestone;

            // Non-payment consequence: Compact reputation drops -10 per unpaid token.
            state.ApplyReputationDelta(TitheFactionId, -TitheRepPenaltyPerToken * cost, "tithe_unpaid");

            state.EmitActionLog("town", "tithe_due", new Dictionary<string, string>
            {
                { "milestone", milestone.ToString() },
                { "cost", cost.ToString() },
                { "activePartySize", activeSize.ToString() },
                { "debt", state.Tithe.Debt.ToString() }
            });
        }
        state.LastUpdate = DateTime.UtcNow;
    }

    /// <summary>
    /// Pay the outstanding tithe at the Bone Clerk. Clears the full token debt; if paid after the
    /// milestone turn it falls due, a 25% gold surcharge applies on the token value. Returns false
    /// when there is no debt or the party cannot cover the tokens (and any gold surcharge). On
    /// success the ongoing tithe penalties lift because the debt is cleared.
    /// </summary>
    public bool PayTithe(GameState state)
    {
        var tithe = state.Tithe;
        if (!tithe.HasDebt) return false;

        var tokensOwed = tithe.Debt;
        var late = tithe.OutstandingSinceTurn.HasValue && state.Overworld.Turns > tithe.OutstandingSinceTurn.Value;
        var goldSurcharge = late
            ? (int)Math.Ceiling(tokensOwed * TitheGoldValuePerToken * TitheLateGoldSurchargeRate)
            : 0;

        if (state.TitheTokens < tokensOwed) return false;
        if (state.PartyGold < goldSurcharge) return false;

        state.TitheTokens -= tokensOwed;
        state.PartyGold -= goldSurcharge;
        tithe.Debt = 0;
        tithe.OutstandingSinceTurn = null;

        state.EmitActionLog("town", "tithe_paid", new Dictionary<string, string>
        {
            { "tokensPaid", tokensOwed.ToString() },
            { "goldSurcharge", goldSurcharge.ToString() },
            { "late", late.ToString().ToLowerInvariant() }
        });
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Re-evaluate faction-exclusive recruits against current reputation: drop any the player no
    /// longer qualifies for (and hasn't recruited) and add newly unlocked ones. Standard recruits
    /// are left untouched.
    /// </summary>
    private static void RefreshExclusiveRecruits(GameState state)
    {
        var eligible = TavernRecruitGenerator.GetExclusiveRecruits(state.Reputation);
        var eligibleIds = eligible.Select(e => e.Id).ToHashSet();

        state.Town.TavernRoster.RemoveAll(r =>
            r.Id.StartsWith(TavernRecruitGenerator.ExclusiveIdPrefix) && !eligibleIds.Contains(r.Id));

        foreach (var ex in eligible)
        {
            if (!state.Town.TavernRoster.Any(r => r.Id == ex.Id))
                state.Town.TavernRoster.Add(ex);
        }
    }

    private void GenerateRumors(GameState state)
    {
        var rng = new GameRandom(state._encounterRng.Roll(1, 10000));
        var count = rng.Roll(3, 5);
        state.Town.Rumors = GenerateRumorsForVisit(rng, count);
    }

    public bool VerifyRumor(GameState state, string rumorId, RumorVerificationSource source)
    {
        var rumor = state.Town.Rumors.FirstOrDefault(r => r.Id == rumorId);
        if (rumor == null || rumor.Verified) return false;

        var rng = new GameRandom(state._encounterRng.Roll(1, 10000));
        var isTrue = VerifyRumor(rumor, source, rng);

        var verifiedRumor = rumor with { Verified = true, VerificationResult = isTrue };
        var index = state.Town.Rumors.FindIndex(r => r.Id == rumorId);
        state.Town.Rumors[index] = verifiedRumor;

        if (rumor.TruthStatus == RumorTruthStatus.True && isTrue)
        {
            state.EmitActionLog("town", "rumor_verified_true", new Dictionary<string, string>
            {
                { "rumorId", rumorId },
                { "source", source.ToString() },
                { "relatedContentId", rumor.RelatedContentId ?? "" }
            });
            if (!string.IsNullOrEmpty(rumor.RelatedContentId))
                state.Journal.Discover(rumor.RelatedContentId);
        }
        else if (rumor.TruthStatus == RumorTruthStatus.Outdated && isTrue)
        {
            state.EmitActionLog("town", "rumor_verified_outdated", new Dictionary<string, string>
            {
                { "rumorId", rumorId },
                { "source", source.ToString() }
            });
        }
        else if (rumor.TruthStatus == RumorTruthStatus.Planted && isTrue)
        {
            state.EmitActionLog("town", "rumor_verified_planted", new Dictionary<string, string>
            {
                { "rumorId", rumorId },
                { "source", source.ToString() }
            });
        }
        else
        {
            state.EmitActionLog("town", "rumor_verified_false", new Dictionary<string, string>
            {
                { "rumorId", rumorId },
                { "source", source.ToString() }
            });
        }

        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool RecruitFromTavern(GameState state, string recruitId)
    {
        var recruit = state.Town.TavernRoster.FirstOrDefault(r => r.Id == recruitId);
        if (recruit == null) return false;

        // Roster cap (active + bench): recruiting at cap is rejected and requires a dismissal first.
        if (state.Party.RosterCount >= PartyState.MaxRosterSize) return false;

        var emptySlot = Array.IndexOf(state.Party.Members, default);
        if (emptySlot < 0) return false;

        var maxHp = EffectiveStats.FromBase(recruit.BaseStats, recruit.Level).MaxHp;
        var character = new CharacterState(
            Guid.NewGuid(), recruit.Name, recruit.ClassId,
            recruit.Level, 0, recruit.BaseStats, maxHp,
            Equipment.Empty, Array.Empty<string>(), 0);

        state.Party.SetMember(emptySlot, character);
        state.Town.TavernRoster.Remove(recruit);
        state.EmitActionLog("roster", "recruited", new Dictionary<string, string>
        {
            { "characterId", character.Id.ToString() },
            { "characterName", character.Name },
            { "classId", character.ClassId },
            { "level", character.Level.ToString() }
        });
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Town-only active&lt;-&gt;bench swap. Rejected outside town (Menu mode), e.g. mid-combat.
    /// Delegates the roster bookkeeping to <see cref="PartyState.SwapActiveBench"/>.
    /// </summary>
    public bool SwapActiveBench(GameState state, int activeSlot, Guid? benchCharacterId)
    {
        if (state.Mode != GameMode.Menu) return false;

        if (!state.Party.SwapActiveBench(activeSlot, benchCharacterId)) return false;

        state.EmitActionLog("roster", "active_bench_swapped", new Dictionary<string, string>
        {
            { "activeSlot", activeSlot.ToString() },
            { "benchCharacterId", (benchCharacterId ?? Guid.Empty).ToString() }
        });
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Town-only dismissal: removes a character from the roster (active or bench), freeing a roster
    /// slot. The character is not added to the dead list. Rejected outside town (Menu mode).
    /// </summary>
    public bool DismissCharacter(GameState state, Guid characterId)
    {
        if (state.Mode != GameMode.Menu) return false;

        if (!state.Party.DismissCharacter(characterId)) return false;

        state.EmitActionLog("roster", "character_dismissed", new Dictionary<string, string>
        {
            { "characterId", characterId.ToString() }
        });
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public ResurrectionResult? ResurrectCharacter(GameState state, Guid characterId)
    {
        var deadIdx = state.Party.DeadCharacters.FindIndex(c => c.Id == characterId);
        if (deadIdx < 0) return null;

        var dead = state.Party.DeadCharacters[deadIdx];
        var attempts = dead.ResurrectionAttempts;
        if (attempts >= 2)
            return new ResurrectionResult(false, "Character is permanently dead.");

        var (goldCost, titheCost, statLoss, branchLock) = attempts switch
        {
            0 => (500, 1, 1, false),
            1 => (1500, 2, 2, true),
            _ => (0, 0, 0, false)
        };

        if (state.PartyGold < goldCost)
            return new ResurrectionResult(false, "Not enough gold.");
        if (state.TitheTokens < titheCost)
            return new ResurrectionResult(false, "Not enough tithe tokens.");

        state.PartyGold -= goldCost;
        state.TitheTokens -= titheCost;

        var newStats = ApplyRandomStatLoss(dead.BaseStats, statLoss);
        var maxHp = EffectiveStats.FromBase(newStats, dead.Level).MaxHp;
        var resurrected = dead with
        {
            BaseStats = newStats,
            CurrentHp = maxHp,
            ResurrectionAttempts = attempts + 1,
            BranchAdvancementLocked = dead.BranchAdvancementLocked || branchLock,
            TempModifiers = Array.Empty<TempStatModifier>()
        };

        var emptySlot = Array.IndexOf(state.Party.Members, default);
        if (emptySlot < 0)
            return new ResurrectionResult(false, "Party is full.");

        state.Party.DeadCharacters.RemoveAt(deadIdx);
        state.Party.SetMember(emptySlot, resurrected);

        state.EmitActionLog("roster", "character_resurrected", new Dictionary<string, string>
        {
            { "characterId", characterId.ToString() },
            { "characterName", resurrected.Name },
            { "attempt", (attempts + 1).ToString() },
            { "goldCost", goldCost.ToString() },
            { "titheCost", titheCost.ToString() },
            { "statLoss", statLoss.ToString() },
            { "branchLocked", branchLock.ToString().ToLowerInvariant() }
        });

        state.LastUpdate = DateTime.UtcNow;
        return new ResurrectionResult(true, null, goldCost, titheCost, statLoss, branchLock, resurrected);
    }

    public bool PurchaseVendorItem(GameState state, string itemId)
    {
        var genericItem = state.Town.VendorStock.FirstOrDefault(v => v.ItemId == itemId);
        if (genericItem != null)
        {
            var price = ApplyTithePenalty(state, ApplyHeatPrice(state, ApplyAllianceDiscount(state, genericItem.Price)));
            return CompletePurchase(state, itemId, price, state.Town.VendorStock, null);
        }

        foreach (var vendor in state.Town.FactionVendors)
        {
            var item = vendor.Stock.FirstOrDefault(v => v.ItemId == itemId);
            if (item != null)
            {
                if (state.Reputation[vendor.FactionId] < vendor.Threshold) continue;
                var price = ApplyTithePenalty(state, ApplyHeatPrice(state, ApplyAllianceDiscount(state, item.Price)));
                return CompletePurchase(state, itemId, price, vendor.Stock, vendor.FactionId);
            }
        }

        return false;
    }

    private static int ApplyAllianceDiscount(GameState state, int price)
    {
        if (!state.IsWildCardAllianceActive) return price;
        return Math.Max(1, (int)(price * 0.75));
    }

    private static int ApplyHeatPrice(GameState state, int price)
    {
        if (!state.Heat.HasPricePenalty) return price;
        return (int)(price * 1.25);
    }

    /// <summary>
    /// While the party owes tithe the Compact restricts fragment supply, raising component/vendor
    /// cost by 50%. Composed after heat/alliance adjustments in <see cref="PurchaseVendorItem"/>.
    /// </summary>
    private static int ApplyTithePenalty(GameState state, int price)
    {
        if (!state.Tithe.HasDebt) return price;
        return (int)Math.Ceiling(price * state.Tithe.ComponentCostMultiplier);
    }

    private static bool CompletePurchase(GameState state, string itemId, int price, List<VendorItem> stock, string? factionId)
    {
        if (state.PartyGold < price) return false;
        state.PartyGold -= price;
        state.PartyInventory.Add(itemId);
        var item = stock.First(v => v.ItemId == itemId);
        stock.Remove(item);
        var payload = new Dictionary<string, string> { { "itemId", itemId }, { "price", price.ToString() } };
        if (factionId != null) payload["factionId"] = factionId;
        state.EmitActionLog("town", "vendor_purchase", payload);
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    private static BaseStats ApplyRandomStatLoss(BaseStats stats, int count)
    {
        var r = Random.Shared;
        var s = stats;
        for (int i = 0; i < count; i++)
        {
            switch (r.Next(5))
            {
                case 0: s = s with { Strength = Math.Max(1, s.Strength - 1) }; break;
                case 1: s = s with { Dexterity = Math.Max(1, s.Dexterity - 1) }; break;
                case 2: s = s with { Constitution = Math.Max(1, s.Constitution - 1) }; break;
                case 3: s = s with { Intelligence = Math.Max(1, s.Intelligence - 1) }; break;
                case 4: s = s with { Willpower = Math.Max(1, s.Willpower - 1) }; break;
            }
        }
        return s;
    }
}
