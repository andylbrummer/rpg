using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Overworld;
using RPC.Engine.Town;

namespace RPC.Engine.Save;

public static class SaveRestorer
{
    public static void RestoreParty(GameState state, SaveData data)
    {
        var party = data.Party ?? Array.Empty<SavePartyMember?>();
        for (int i = 0; i < 6; i++)
        {
            if (i < party.Length && party[i] is { } s)
            {
                var level = Math.Max(1, s.Level);
                var xp = Math.Max(0, s.Xp);
                var hp = Math.Max(0, s.CurrentHp);
                var row = Math.Clamp(s.Row, 0, 1);
                var componentInventory = s.ComponentInventory?.Select(c => new ComponentStack(c.ItemId, c.Count, c.MaxStack)).ToArray() ?? Array.Empty<ComponentStack>();
                state.Party.SetMember(i, new CharacterState(
                    s.Id, s.Name, s.ClassId, level, xp,
                    s.BaseStats, hp, s.Equipment,
                    s.KnownAbilities, row, s.BranchChoice, s.BranchLevel6,
                    s.TempModifiers, s.ResurrectionAttempts, s.BranchAdvancementLocked,
                    componentInventory));
            }
            else
            {
                state.Party.SetMember(i, default);
            }
        }
    }

    public static void RestorePlayer(GameState state, SaveData data)
    {
        if (Enum.TryParse<Direction>(data.Player.Facing, out var facing))
        {
            state.Player = new Player(
                new Position(data.Player.X, data.Player.Y),
                facing);
        }
    }

    public static void RestoreExploredTiles(GameState state, SaveData data)
    {
        state.ExploredTiles.Clear();
        foreach (var tile in data.ExploredTiles)
            state.ExploredTiles.Add(tile);
    }

    public static void RestoreMode(GameState state, SaveData data)
    {
        if (Enum.TryParse<GameMode>(data.Mode, out var mode))
            state.Mode = mode;
    }

    public static void RestoreTown(GameState state, SaveData data)
    {
        if (data.Town != null)
        {
            state.Town.CurrentTownId = data.Town.CurrentTownId;
            state.Town.AvailableMissions = data.Town.AvailableMissions
                .Select(m => new MissionOffer(m.Id, m.Title, m.Description, m.MinLevel, m.Rewards, m.RepReward, m.FactionId))
                .ToList();
            state.Town.VendorStock = data.Town.VendorStock
                .Select(v => new VendorItem(v.ItemId, v.Name, v.Price, v.Quantity))
                .ToList();
            state.Town.FactionVendors = data.Town.FactionVendors
                .Select(fv => new FactionVendor(
                    fv.FactionId,
                    fv.Name,
                    fv.Threshold,
                    fv.Stock.Select(v => new VendorItem(v.ItemId, v.Name, v.Price, v.Quantity)).ToList()))
                .ToList();
            state.Town.FactionContacts = data.Town.FactionContacts
                .Select(c => new FactionContact(c.Id, c.Name, c.FactionId, c.Portrait))
                .ToList();
            state.Town.TavernRoster = data.Town.TavernRoster
                .Select(r => new TavernRecruit(r.Id, r.Name, r.ClassId, r.Level, r.BaseStats, r.Cost))
                .ToList();
            state.Town.ViewedMissions = data.Town.ViewedMissions.ToList();
            state.Town.QuestLog = data.Town.QuestLog
                .Select(q => new ActiveMission(q.Id, q.Title, q.Description, q.RepReward, q.FactionId,
                    Enum.TryParse<MissionStatus>(q.Status, true, out var status) ? status : MissionStatus.Active))
                .ToList();
            state.Town.Rumors = data.Town.Rumors
                .Select(r => new TownRumor(
                    r.Id,
                    r.Text,
                    Enum.TryParse<RumorTruthStatus>(r.TruthStatus, true, out var ts) ? ts : RumorTruthStatus.True,
                    r.Verified,
                    r.VerificationResult,
                    r.RelatedContentId,
                    r.RelatedFactionId))
                .ToList();
        }
    }

    public static void RestoreActionLog(GameState state, SaveData data)
    {
        state.RestoreActionLog(
            (data.ActionLog ?? Array.Empty<SaveActionLogEntry>())
            .Select(e => new ActionLogEntry(e.Turn, e.Category, e.Type, e.Payload))
            .ToList());
    }

    public static void RestoreDungeonType(GameState state, SaveData data)
    {
        state.CurrentDungeonType = data.DungeonType;
    }

    public static void RestoreReputation(GameState state, SaveData data)
    {
        state.Reputation.Clear();
        foreach (var kv in data.Reputation)
        {
            state.Reputation[kv.Key] = Math.Clamp(kv.Value, -100, 100);
        }
    }

    public static void RestoreSettings(GameState state, SaveData data)
    {
        state.SettingsHash = data.Settings;
    }

    public static void RestoreJournal(GameState state, SaveData data)
    {
        state.Journal.DiscoveryOrder.Clear();
        state.Journal.Discovered.Clear();
        if (data.Journal?.DiscoveredSynergies != null)
        {
            foreach (var id in data.Journal.DiscoveredSynergies)
            {
                state.Journal.Discover(id);
            }
        }
    }

    public static void RestoreHeat(GameState state, SaveData data)
    {
        if (data.Heat != null)
        {
            state.Heat.Value = data.Heat.Value;
        }
    }

    public static void RestoreCampaignConfig(GameState state, SaveData data)
    {
        if (data.CampaignConfig == null) return;

        if (!Enum.TryParse<SchemeType>(data.CampaignConfig.Scheme, out var scheme))
            scheme = SchemeType.BloomHarvest;
        if (!Enum.TryParse<ComplicationType>(data.CampaignConfig.Complication, out var complication))
            complication = ComplicationType.BloomSiege;

        state.CampaignConfig = new CampaignConfig
        {
            Patron = data.CampaignConfig.Patron,
            Threat = data.CampaignConfig.Threat,
            Mastermind = data.CampaignConfig.Mastermind,
            Scheme = scheme,
            WildCard = data.CampaignConfig.WildCard,
            Complication = complication,
            EvidenceChain = data.CampaignConfig.EvidenceChain.ToList(),
            FactionTimelines = data.CampaignConfig.FactionTimelines.ToDictionary(
                kv => kv.Key,
                kv => new FactionTimeline(kv.Value.Preparing, kv.Value.Executing)),
            NpcCasting = new Dictionary<string, string>(data.CampaignConfig.NpcCasting),
            WildcardTrigger = data.CampaignConfig.WildcardTrigger == null ? null : new WildcardTrigger(
                data.CampaignConfig.WildcardTrigger.FactionId,
                data.CampaignConfig.WildcardTrigger.TurnThreshold)
        };
    }

    public static void RestoreOverworld(GameState state, SaveData data)
    {
        state.Overworld.Turns = Math.Max(0, data.OverworldTurns);
        state.Overworld.CurrentNodeId = string.IsNullOrEmpty(data.OverworldCurrentNodeId) ? "the_reach" : data.OverworldCurrentNodeId;

        if (data.OverworldNodes?.Length > 0)
        {
            state.Overworld.Nodes.Clear();
            foreach (var n in data.OverworldNodes)
            {
                if (Enum.TryParse<NodeType>(n.Type, out var nodeType))
                {
                    state.Overworld.Nodes[n.Id] = new OverworldNode(n.Id, n.Name, nodeType)
                    {
                        FactionPresence = n.FactionPresence?.ToList() ?? new List<string>(),
                        DungeonTemplateId = n.DungeonTemplateId
                    };
                }
            }
        }

        if (data.OverworldRoutes?.Length > 0)
        {
            state.Overworld.Routes.Clear();
            foreach (var r in data.OverworldRoutes)
            {
                if (Enum.TryParse<RouteStatus>(r.Status, out var status))
                {
                    state.Overworld.Routes.Add(new OverworldRoute(r.From, r.To, r.Distance, r.DangerRating, r.Terrain)
                    {
                        Status = status
                    });
                }
            }
        }

        state.CampaignEnded = data.CampaignEnded;
        state.PartyGold = data.PartyGold;
        state.TitheTokens = data.TitheTokens;
        state.PartyInventory = data.PartyInventory?.ToList() ?? new List<string>();

        state.Party.ExpeditionCache = data.ExpeditionCache?.Select(c => new ComponentStack(c.ItemId, c.Count, c.MaxStack)).ToArray() ?? Array.Empty<ComponentStack>();
        state.Party.DeadCharacters.Clear();
        if (data.DeadCharacters != null)
        {
            foreach (var d in data.DeadCharacters)
            {
                var deadInventory = d.ComponentInventory?.Select(c => new ComponentStack(c.ItemId, c.Count, c.MaxStack)).ToArray() ?? Array.Empty<ComponentStack>();
                state.Party.DeadCharacters.Add(new CharacterState(
                    d.Id, d.Name, d.ClassId,
                    Math.Max(1, d.Level), Math.Max(0, d.Xp),
                    d.BaseStats, 0, d.Equipment,
                    d.KnownAbilities, Math.Clamp(d.Row, 0, 1),
                    d.BranchChoice, d.BranchLevel6,
                    d.TempModifiers, d.ResurrectionAttempts, d.BranchAdvancementLocked,
                    deadInventory));
            }
        }
        state.SetAccusedFaction(data.AccusedFaction);
        state.SetMastermindAdvantage(data.MastermindAdvantage);
        state.SetFinalDungeonUnlocked(data.FinalDungeonUnlocked);
    }

    public static void RestoreEvidence(GameState state, SaveData data)
    {
        state.Evidence.Clear();
        foreach (var kv in data.Evidence ?? new Dictionary<string, int>())
        {
            state.Evidence.SetCounter(kv.Key, kv.Value);
        }
        if (!string.IsNullOrEmpty(data.SuspectedFaction))
        {
            state.Evidence.SetSuspectedFaction(data.SuspectedFaction);
        }
    }

    public static void RestoreWorldState(GameState state, SaveData data)
    {
        if (data.WorldState != null)
        {
            state.WorldState.Settlements = new Dictionary<string, string>(data.WorldState.Settlements);
            state.WorldState.AccessibleDungeons = data.WorldState.AccessibleDungeons?.ToList() ?? new List<string>();
            state.WorldState.FactionTerritory = data.WorldState.FactionTerritory?.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ToList()) ?? new Dictionary<string, List<string>>();
        }
    }

    public static void RestoreDowntime(GameState state, SaveData data)
    {
        var ids = (data.DowntimeCompleted ?? Array.Empty<string>())
            .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();
        state.RestoreDowntimeState(ids);
    }

    public static void RestoreWildCardAlliance(GameState state, SaveData data)
    {
        if (!string.IsNullOrEmpty(data.WildCardAllianceStatus) &&
            Enum.TryParse<WildCardAllianceStatus>(data.WildCardAllianceStatus, out var status))
        {
            state.WildCardAllianceStatus = status;
            state.WildCardAllianceTurn = data.WildCardAllianceTurn;
        }
        else
        {
            state.WildCardAllianceStatus = WildCardAllianceStatus.None;
            state.WildCardAllianceTurn = 0;
        }
    }

    public static void RestoreStepsSinceEncounter(GameState state, SaveData data)
    {
        state.StepsSinceEncounter = Math.Max(0, data.StepsSinceEncounter);
    }
}
