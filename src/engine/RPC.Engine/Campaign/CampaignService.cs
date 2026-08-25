using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Overworld;
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
            var modifier = state.Campaign.FactionTimelineModifiers.GetValueOrDefault(factionId, 0);
            var preparingTurn = Math.Max(1, timeline.Preparing + modifier);
            var executingTurn = Math.Max(preparingTurn + 1, timeline.Executing + modifier);

            if (state.Overworld.Turns >= executingTurn)
                return FactionState.Executing;
            if (state.Overworld.Turns >= preparingTurn)
                return FactionState.Preparing;
        }
        return FactionState.Investigating;
    }

    public void ModifyFactionTimeline(GameState state, string factionId, int delta)
    {
        if (state.CampaignConfig?.FactionTimelines.TryGetValue(factionId, out var timeline) != true)
            return;

        var current = state.Campaign.FactionTimelineModifiers.GetValueOrDefault(factionId, 0);
        var clamped = Math.Max(-3, Math.Min(3, current + delta));
        if (clamped == current) return;

        state.Campaign.FactionTimelineModifiers[factionId] = clamped;
        state.EmitActionLog("faction", "timeline_modified", new Dictionary<string, string>
        {
            { "factionId", factionId },
            { "delta", delta.ToString() },
            { "totalModifier", clamped.ToString() }
        });
        state.LastUpdate = DateTime.UtcNow;
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
        if (state.FinalDungeonUnlocked) return false;

        var mastermind = state.CampaignConfig.Mastermind;

        if (state.Campaign.BetrayalPath)
        {
            // Betrayal path: high rep with mastermind faction unlocks the finale
            var mastermindRep = state.Reputation[mastermind];
            if (mastermindRep < 20)
                return false;

            state.FinalDungeonUnlocked = true;
            state.EmitActionLog("mastermind", "final_dungeon_unlocked", new Dictionary<string, string>
            {
                { "mastermind", mastermind },
                { "betrayal", "true" }
            });
            state.EmitActionLog("narrative", "scheme_alliance", new Dictionary<string, string>
            {
                { "mastermind", mastermind },
                { "scheme", state.CampaignConfig.Scheme.ToString() }
            });
        }
        else
        {
            // Standard path: accuse mastermind with evidence
            if (state.AccusedFaction != mastermind) return false;
            if (!state.Evidence.Counters.Values.Any(v => v >= 10)) return false;

            state.FinalDungeonUnlocked = true;
            state.EmitActionLog("mastermind", "final_dungeon_unlocked", new Dictionary<string, string>
            {
                { "mastermind", mastermind }
            });
            state.EmitActionLog("narrative", "scheme_exposed", new Dictionary<string, string>
            {
                { "mastermind", mastermind },
                { "scheme", state.CampaignConfig.Scheme.ToString() }
            });
        }

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

    public bool DiscoverSecret(GameState state, string? secretType, string secretId, string trigger = "manual")
    {
        if (string.IsNullOrEmpty(secretId)) return false;
        if (state.Journal.IsDiscovered(secretId)) return false; // already found — idempotent

        secretType ??= "unknown";

        // Bloodline gate: a secret carrying a BloodlineRequirement is only discoverable by a party
        // whose family name matches (case-insensitive). A mismatch refuses discovery and logs the
        // lock rather than the discovery.
        var def = state.Secrets.Get(secretId);
        if (!string.IsNullOrEmpty(def?.BloodlineRequirement)
            && !string.Equals(def.BloodlineRequirement, state.Campaign.FamilyName, StringComparison.OrdinalIgnoreCase))
        {
            state.EmitActionLog("dungeon", "secret_bloodline_locked", new Dictionary<string, string>
            {
                { "secretType", secretType },
                { "secretId", secretId },
                { "requiredBloodline", def.BloodlineRequirement! },
                { "trigger", trigger }
            });
            state.LastUpdate = DateTime.UtcNow;
            return false;
        }

        state.Journal.Discover(secretId);
        state.Analytics.RecordSecretDiscovered(secretId);
        state.EmitActionLog("dungeon", "secret_discovered", new Dictionary<string, string>
        {
            { "secretType", secretType },
            { "secretId", secretId },
            { "trigger", trigger }
        });
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Mark a secret as detected-but-unrevealed: the party senses something is there (an automap
    /// "?") without yet knowing its nature. Idempotent, and a no-op once the secret is fully
    /// discovered. Detection senses physical presence, so it ignores the bloodline gate — a
    /// bloodline-locked secret can still be sensed, just not opened. Returns true when this call
    /// newly detected the secret.
    /// </summary>
    public bool DetectSecret(GameState state, string secretId, string trigger = "manual")
    {
        if (string.IsNullOrEmpty(secretId)) return false;
        if (state.Journal.IsDiscovered(secretId)) return false; // already fully known
        if (state.Journal.IsDetected(secretId)) return false;   // already sensed — idempotent

        var def = state.Secrets.Get(secretId);
        state.Journal.Detect(secretId);
        state.EmitActionLog("dungeon", "secret_detected", new Dictionary<string, string>
        {
            { "secretId", secretId },
            { "secretType", def?.Type ?? "unknown" },
            { "trigger", trigger }
        });
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Mark a lore document as read and passively reveal every still-hidden secret it hints at.
    /// Idempotent per document. Returns the secret ids newly discovered by this read.
    /// </summary>
    public IReadOnlyList<string> ReadDocument(GameState state, string documentId)
    {
        if (string.IsNullOrEmpty(documentId)) return Array.Empty<string>();
        if (!state.Campaign.ReadDocuments.Add(documentId))
            return Array.Empty<string>(); // already read

        state.Analytics.RecordDocumentRead(documentId);
        state.EmitActionLog("dungeon", "document_read", new Dictionary<string, string>
        {
            { "documentId", documentId }
        });

        var discovered = new List<string>();
        foreach (var secretId in state.Secrets.SecretsForDocument(documentId))
        {
            var secret = state.Secrets.Get(secretId);
            if (secret is null) continue;
            if (DiscoverSecret(state, secret.Type, secret.Id, "document"))
                discovered.Add(secret.Id);
        }

        state.LastUpdate = DateTime.UtcNow;
        return discovered;
    }

    /// <summary>
    /// Read a Family Archive: grant its faction intel (reputation, evidence, journal entry) on the
    /// first read. Idempotent per archive — shares the campaign's document-read tracking set so an
    /// archive grants its intel exactly once. Returns the granted result, or null if the archive is
    /// unknown or already read.
    /// </summary>
    public ArchiveReadResult? ReadArchive(GameState state, string archiveId)
    {
        if (string.IsNullOrEmpty(archiveId)) return null;
        var archive = state.Archives.Get(archiveId);
        if (archive is null) return null;
        if (!state.Campaign.ReadDocuments.Add(archiveId))
            return null; // already read — idempotent

        if (archive.RepReward != 0)
            ApplyReputationDelta(state, archive.FactionId, archive.RepReward, "family_archive");
        if (archive.EvidenceReward > 0)
            AddEvidence(state, archive.FactionId, "family_archive", archive.EvidenceReward);
        if (!string.IsNullOrEmpty(archive.JournalEntryId))
            state.Journal.Discover(archive.JournalEntryId);

        state.Analytics.RecordDocumentRead(archiveId);
        state.EmitActionLog("dungeon", "archive_read", new Dictionary<string, string>
        {
            { "archiveId", archiveId },
            { "factionId", archive.FactionId },
            { "repReward", archive.RepReward.ToString() },
            { "evidenceReward", archive.EvidenceReward.ToString() },
            { "journalEntry", archive.JournalEntryId ?? "" }
        });
        state.LastUpdate = DateTime.UtcNow;
        return new ArchiveReadResult(archiveId, archive.FactionId, archive.RepReward, archive.EvidenceReward, archive.JournalEntryId);
    }

    // ---- Settlement fate system ----
    // Settlements progress from Contested to a terminal fate (Saved/Lost/Abandoned) either by
    // explicit player choice or by a campaign roll driven by Heat + faction pressure. Terminal
    // fates are locked; rolls only act on still-Contested settlements. The fate set feeds the
    // epilogue and the world-state queries below.

    /// <summary>Track a settlement as Contested if it is not already known. No-op once registered.</summary>
    public void RegisterSettlement(GameState state, string settlementId)
    {
        if (string.IsNullOrWhiteSpace(settlementId)) return;
        if (state.WorldState.Settlements.ContainsKey(settlementId)) return;
        state.WorldState.Settlements[settlementId] = SettlementFate.Contested;
    }

    public void ChooseSettlementFate(GameState state, string settlementId, string fate)
    {
        var previous = state.WorldState.Settlements.GetValueOrDefault(settlementId, SettlementFate.Contested);
        state.WorldState.Settlements[settlementId] = fate;
        state.EmitActionLog("dungeon", "settlement_fate_chosen", new Dictionary<string, string>
        {
            { "settlementId", settlementId },
            { "fate", fate },
            { "previousFate", previous },
            { "source", "player_choice" }
        });
        state.LastUpdate = DateTime.UtcNow;
    }

    /// <summary>
    /// Resolve a single Contested settlement by campaign roll. Already-terminal settlements are
    /// left untouched and their current fate returned.
    /// </summary>
    public string RollSettlementFate(GameState state, string settlementId, GameRandom rng)
    {
        var current = SettlementFate.Normalize(state.WorldState.Settlements.GetValueOrDefault(settlementId));
        if (SettlementFate.IsTerminal(current)) return current;

        var pressure = SettlementPressure(state);
        var roll = rng.Roll(1, 100);
        var fate = roll <= pressure / 2 ? SettlementFate.Lost
            : roll <= pressure ? SettlementFate.Abandoned
            : SettlementFate.Saved;

        state.WorldState.Settlements[settlementId] = fate;
        state.EmitActionLog("dungeon", "settlement_fate_rolled", new Dictionary<string, string>
        {
            { "settlementId", settlementId },
            { "fate", fate },
            { "pressure", pressure.ToString() },
            { "roll", roll.ToString() },
            { "source", "campaign_roll" }
        });
        state.LastUpdate = DateTime.UtcNow;
        return fate;
    }

    /// <summary>
    /// Seed every overworld town as a tracked settlement, then roll a fate for each one still
    /// Contested. Returns the number of settlements resolved. Called as the campaign concludes.
    /// </summary>
    public int RollPendingSettlementFates(GameState state, GameRandom rng)
    {
        foreach (var node in state.Overworld.Nodes.Values)
        {
            if (node.Type == NodeType.Town)
                RegisterSettlement(state, node.Id);
        }

        var pending = state.WorldState.Settlements
            .Where(kv => !SettlementFate.IsTerminal(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in pending)
            RollSettlementFate(state, id, rng);

        return pending.Count;
    }

    /// <summary>Campaign pressure (0-100) that biases settlement rolls toward Lost/Abandoned.</summary>
    private int SettlementPressure(GameState state)
    {
        var pressure = state.Heat.Value;
        var anyExecuting = CampaignConfig.FactionPool.Any(f => GetFactionState(state, f) == FactionState.Executing);
        if (anyExecuting) pressure += 20;
        return Math.Clamp(pressure, 0, 100);
    }

    public string GetSettlementFate(GameState state, string settlementId) =>
        SettlementFate.Normalize(state.WorldState.Settlements.GetValueOrDefault(settlementId));

    public IReadOnlyList<string> GetSettlementsByFate(GameState state, string fate)
    {
        var target = SettlementFate.Normalize(fate);
        return state.WorldState.Settlements
            .Where(kv => SettlementFate.Normalize(kv.Value) == target)
            .Select(kv => kv.Key)
            .ToList();
    }

    public IReadOnlyDictionary<string, int> GetSettlementFateCounts(GameState state)
    {
        var counts = new Dictionary<string, int>
        {
            [SettlementFate.Saved] = 0,
            [SettlementFate.Lost] = 0,
            [SettlementFate.Abandoned] = 0,
            [SettlementFate.Contested] = 0
        };
        foreach (var raw in state.WorldState.Settlements.Values)
            counts[SettlementFate.Normalize(raw)]++;
        return counts;
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
        state.Analytics.RecordBranchChosen(member.ClassId, resolvedBranch, levelLabel == "3" ? 3 : 6);

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

    /// <summary>
    /// Checks faction reputation conditions and unlocks optional dungeons.
    /// Call after reputation changes or on turn increments.
    /// </summary>
    public bool ChooseBetrayal(GameState state)
    {
        if (state.Campaign.BetrayalPath)
            return false;

        // Prerequisite: must have evidence about the mastermind to prove you know what you're joining
        var mastermind = state.CampaignConfig?.Mastermind;
        if (string.IsNullOrEmpty(mastermind))
            return false;
        if (!state.Evidence.Counters.TryGetValue(mastermind, out var evidenceCount) || evidenceCount < 1)
            return false;

        state.Campaign.BetrayalPath = true;
        state.EmitActionLog("campaign", "betrayal_chosen", new Dictionary<string, string>
        {
            { "mastermind", mastermind },
            { "evidence", evidenceCount.ToString() }
        });
        return true;
    }

    public void CheckOptionalDungeons(GameState state, IReadOnlyDictionary<string, DungeonTemplate> dungeonTemplates)
    {
        foreach (var (id, template) in dungeonTemplates)
        {
            if (template.UnlockConditions is null || template.UnlockConditions.Length == 0)
                continue;
            if (state.Campaign.UnlockedDungeons.Contains(id))
                continue;

            bool allMet = template.UnlockConditions.All(uc =>
                state.Campaign.Reputation[uc.FactionId] >= uc.MinReputation);

            if (allMet)
            {
                state.Campaign.UnlockedDungeons.Add(id);
                state.Overworld.Nodes[id] = new OverworldNode(id, template.Name, NodeType.Dungeon)
                {
                    DungeonTemplateId = id
                };
                state.Analytics.RecordOptionalDungeonUnlocked(id);
                state.EmitActionLog("world", "dungeon_unlocked", new Dictionary<string, string>
                {
                    { "dungeonId", id },
                    { "dungeonName", template.Name }
                });
            }
        }
    }
}
