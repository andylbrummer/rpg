using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Town;

namespace RPC.Engine.Campaign;

public class CampaignService
{
    private readonly ClassRegistry? _classRegistry;

    public CampaignService(ClassRegistry? classRegistry)
    {
        _classRegistry = classRegistry;
    }

    public FactionState GetFactionState(GameState state, string factionId)
    {
        if (state.CampaignConfig?.FactionTimelines.TryGetValue(factionId, out var timeline) == true)
        {
            if (state.Overworld.Turns >= timeline.Executing)
                return FactionState.Executing;
            if (state.Overworld.Turns >= timeline.Preparing)
                return FactionState.Preparing;
        }
        return FactionState.Investigating;
    }

    public bool CheckWildCardTrigger(GameState state)
    {
        if (state.CampaignConfig?.WildcardTrigger == null) return false;
        if (state.WildCardAllianceStatus != WildCardAllianceStatus.None) return false;
        if (state.Overworld.Turns < state.CampaignConfig.WildcardTrigger.TurnThreshold) return false;
        if (state.Reputation[state.CampaignConfig.WildcardTrigger.FactionId] < 20) return false;

        state.WildCardAllianceStatus = WildCardAllianceStatus.Offered;
        state.WildCardAllianceTurn = state.Overworld.Turns;
        state.EmitActionLog("campaign", "wildcard_alliance_offered", new Dictionary<string, string>
        {
            { "factionId", state.CampaignConfig.WildcardTrigger.FactionId },
            { "turn", state.Overworld.Turns.ToString() }
        });
        return true;
    }

    public bool AcceptWildCardAlliance(GameState state)
    {
        if (state.WildCardAllianceStatus != WildCardAllianceStatus.Offered) return false;
        state.WildCardAllianceStatus = WildCardAllianceStatus.Accepted;
        state.EmitActionLog("campaign", "wildcard_alliance_accepted", new Dictionary<string, string>
        {
            { "factionId", state.CampaignConfig?.WildcardTrigger?.FactionId ?? "" }
        });
        // Add unique questline mission
        if (state.CampaignConfig?.WildcardTrigger != null)
        {
            var factionId = state.CampaignConfig.WildcardTrigger.FactionId;
            state.Town.QuestLog.Add(new ActiveMission(
                $"wildcard_quest_{factionId}",
                "The Wild Card's Gambit",
                $"The {factionId} has offered you a unique opportunity. Complete their special assignment to cement the alliance.",
                25,
                factionId,
                MissionStatus.Active));
        }
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool RefuseWildCardAlliance(GameState state)
    {
        if (state.WildCardAllianceStatus != WildCardAllianceStatus.Offered) return false;
        state.WildCardAllianceStatus = WildCardAllianceStatus.Refused;
        state.EmitActionLog("campaign", "wildcard_alliance_refused", new Dictionary<string, string>
        {
            { "factionId", state.CampaignConfig?.WildcardTrigger?.FactionId ?? "" }
        });
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool IgnoreWildCardAlliance(GameState state)
    {
        if (state.WildCardAllianceStatus != WildCardAllianceStatus.Offered) return false;
        state.WildCardAllianceStatus = WildCardAllianceStatus.Ignored;
        state.EmitActionLog("campaign", "wildcard_alliance_ignored", new Dictionary<string, string>
        {
            { "factionId", state.CampaignConfig?.WildcardTrigger?.FactionId ?? "" }
        });
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public void ApplyReputationDelta(GameState state, string factionId, int delta, string source)
    {
        var changes = state.Reputation.ApplyDelta(factionId, delta, source);
        foreach (var change in changes)
        {
            state.EmitActionLog("faction", "rep_changed", new Dictionary<string, string>
            {
                { "factionId", change.FactionId },
                { "delta", change.Delta.ToString() },
                { "newValue", change.NewValue.ToString() },
                { "source", change.Source }
            });
        }
        state.LastUpdate = DateTime.UtcNow;
    }

    public void AddEvidence(GameState state, string factionId, string source, int amount = 1)
    {
        var result = state.Evidence.AddEvidence(factionId, source, amount);
        state.EmitActionLog("evidence", "evidence_added", new Dictionary<string, string>
        {
            { "factionId", result.FactionId },
            { "amount", result.Amount.ToString() },
            { "newValue", result.NewValue.ToString() },
            { "source", result.Source },
            { "threshold", result.ThresholdReached.ToString() }
        });
        state.LastUpdate = DateTime.UtcNow;
    }

    public bool AccuseFaction(GameState state, string factionId)
    {
        if (state.CampaignConfig == null) return false;
        if (state.Evidence.GetThreshold(factionId) < 7) return false;
        if (state.AccusedFaction != null) return false;

        state.AccusedFaction = factionId;
        state.EmitActionLog("narrative", "mastermind_accused", new Dictionary<string, string>
        {
            { "factionId", factionId },
            { "mastermind", state.CampaignConfig.Mastermind }
        });
        var isCorrect = factionId == state.CampaignConfig.Mastermind;

        if (isCorrect)
        {
            state.EmitActionLog("mastermind", "accusation_correct", new Dictionary<string, string>
            {
                { "factionId", factionId }
            });
        }
        else
        {
            state.MastermindAdvantage = true;
            ApplyReputationDelta(state, factionId, -20, "wrong_accusation");
            state.EmitActionLog("mastermind", "accusation_wrong", new Dictionary<string, string>
            {
                { "factionId", factionId },
                { "penalty", "-20" }
            });
        }

        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool UnlockFinalDungeon(GameState state)
    {
        if (state.CampaignConfig == null) return false;
        if (state.AccusedFaction != state.CampaignConfig.Mastermind) return false;
        if (!state.Evidence.Counters.Values.Any(v => v >= 10)) return false;
        if (state.FinalDungeonUnlocked) return false;

        state.FinalDungeonUnlocked = true;
        state.EmitActionLog("mastermind", "final_dungeon_unlocked", new Dictionary<string, string>
        {
            { "mastermind", state.CampaignConfig.Mastermind }
        });
        state.EmitActionLog("narrative", "scheme_exposed", new Dictionary<string, string>
        {
            { "mastermind", state.CampaignConfig.Mastermind },
            { "scheme", state.CampaignConfig.Scheme.ToString() }
        });
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool ChooseBranch(GameState state, Guid characterId, string branch)
    {
        var member = state.Party.Members.FirstOrDefault(m => m.Id == characterId);
        if (member.Id == Guid.Empty || member.Level < 3) return false;
        if (_classRegistry?.Get(member.ClassId) is not { } classDef) return false;

        if (member.BranchChoice == null && TryResolveLevel3Branch(member, branch, classDef, out var resolved3))
        {
            ApplyBranchToMember(state, member, resolved3, "3", classDef);
            return true;
        }

        if (member.Level >= 6 && member.BranchLevel6 == null && TryResolveLevel6Branch(state, member, branch, classDef, out var resolved6))
        {
            ApplyBranchToMember(state, member, resolved6, "6", classDef);
            return true;
        }

        return false;
    }

    public void DiscoverSecret(GameState state, string secretType, string secretId)
    {
        state.EmitActionLog("dungeon", "secret_discovered", new Dictionary<string, string>
        {
            { "secretType", secretType },
            { "secretId", secretId }
        });
        state.LastUpdate = DateTime.UtcNow;
    }

    public void ChooseSettlementFate(GameState state, string settlementId, string fate)
    {
        state.WorldState.Settlements[settlementId] = fate;
        state.EmitActionLog("dungeon", "settlement_fate_chosen", new Dictionary<string, string>
        {
            { "settlementId", settlementId },
            { "fate", fate }
        });
        state.LastUpdate = DateTime.UtcNow;
    }

    public bool ApplyDialogueReputation(GameState state, string factionId, int delta)
    {
        ApplyReputationDelta(state, factionId, delta, "dialogue_choice");
        return true;
    }

    public void SetReputation(GameState state, string factionId, int value)
    {
        state.Reputation[factionId] = value;
        state.LastUpdate = DateTime.UtcNow;
    }

    private static bool TryResolveLevel3Branch(CharacterState member, string branch, ClassDef classDef, out string resolvedBranch)
    {
        resolvedBranch = branch;
        var available = classDef.AvailableBranches ?? classDef.Branches?.Where(b => b.RequiresBranch == null).Select(b => b.Id).ToArray() ?? Array.Empty<string>();
        return available.Contains(branch);
    }

    private static bool TryResolveLevel6Branch(GameState state, CharacterState member, string branch, ClassDef classDef, out string resolvedBranch)
    {
        resolvedBranch = branch;
        var available = classDef.Branches?.Where(b => b.RequiresBranch == member.BranchChoice).Select(b => b.Id).ToArray() ?? Array.Empty<string>();
        if (!available.Contains(branch)) return false;

        var branchDef = classDef.Branches?.FirstOrDefault(b => b.Id == branch);
        if (branchDef?.FactionGate is { } gate && state.Reputation[gate.FactionId] < gate.Threshold)
        {
            var fallback = branchDef.FallbackBranch;
            if (string.IsNullOrEmpty(fallback)) return false;
            resolvedBranch = fallback;

            state.EmitActionLog("branch", "branch_fallback", new Dictionary<string, string>
            {
                { "characterId", member.Id.ToString() },
                { "originalBranch", branch },
                { "fallbackBranch", resolvedBranch },
                { "factionId", gate.FactionId },
                { "threshold", gate.Threshold.ToString() }
            });
        }
        return true;
    }

    private static void ApplyBranchToMember(GameState state, CharacterState member, string resolvedBranch, string levelLabel, ClassDef classDef)
    {
        var branchAbilities = classDef.Abilities
            .Where(a => a.Branch == resolvedBranch)
            .Select(a => a.Id)
            .ToArray();

        var newAbilities = member.KnownAbilities
            .Concat(branchAbilities)
            .Distinct()
            .ToArray();

        var index = Array.IndexOf(state.Party.Members, member);
        state.Party.SetMember(index, levelLabel == "3"
            ? member with { BranchChoice = resolvedBranch, KnownAbilities = newAbilities }
            : member with { BranchLevel6 = resolvedBranch, KnownAbilities = newAbilities });

        state.EmitActionLog("branch", "branch_chosen", new Dictionary<string, string>
        {
            { "characterId", member.Id.ToString() },
            { "branch", resolvedBranch },
            { "level", levelLabel }
        });

        state.LastUpdate = DateTime.UtcNow;
    }
}
