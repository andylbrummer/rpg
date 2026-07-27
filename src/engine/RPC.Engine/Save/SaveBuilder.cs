using RPC.Engine.Character;
using RPC.Engine.Town;

namespace RPC.Engine.Save;

/// <summary>
/// Build/serialize half of the save system. Mirrors <see cref="SaveRestorer"/>:
/// each feature exposes a Build&lt;Feature&gt; helper so save serialization is
/// discoverable by feature name. <see cref="Build"/> composes them into the
/// top-level <see cref="SaveData"/> aggregate.
/// </summary>
public static class SaveBuilder
{
    /// <summary>Current save schema version produced by the builder.</summary>
    public const int CurrentSchemaVersion = 13;

    public static SaveData Build(GameState state)
    {
        return new SaveData
        {
            SchemaVersion = CurrentSchemaVersion,
            Party = BuildParty(state),
            Player = BuildPlayer(state),
            DungeonType = state.CurrentDungeonType,
            ExploredTiles = state.ExploredTiles.AsEnumerable().ToArray(),
            Mode = state.Mode.ToString(),
            Town = BuildTown(state),
            ActionLog = BuildActionLog(state),
            Reputation = new Dictionary<string, int>(state.Reputation),
            Evidence = new Dictionary<string, int>(state.Evidence.Counters),
            SuspectedFaction = state.Evidence.SuspectedFaction,
            Settings = state.SettingsHash,
            PartyGold = state.PartyGold,
            TitheTokens = state.TitheTokens,
            TitheDebt = state.Tithe.Debt,
            TitheBilledMilestones = state.Tithe.BilledMilestones.ToArray(),
            TitheOutstandingSinceTurn = state.Tithe.OutstandingSinceTurn,
            PartyInventory = state.PartyInventory.ToArray(),
            ExpeditionCache = BuildExpeditionCache(state),
            TownStorage = BuildTownStorage(state),
            CollectedLoot = state.Exploration.CollectedLoot.ToArray(),
            DeadCharacters = BuildDeadCharacters(state),
            Bench = BuildBench(state),
            OverworldTurns = state.Overworld.Turns,
            OverworldCurrentNodeId = state.Overworld.CurrentNodeId,
            CampaignEnded = state.CampaignEnded,
            AccusedFaction = state.AccusedFaction,
            MastermindAdvantage = state.MastermindAdvantage,
            FinalDungeonUnlocked = state.FinalDungeonUnlocked,
            Journal = BuildJournal(state),
            Heat = BuildHeat(state),
            CampaignConfig = BuildCampaignConfig(state),
            OverworldNodes = BuildOverworldNodes(state),
            OverworldRoutes = BuildOverworldRoutes(state),
            CurrentAct = state.CurrentAct,
            WorldState = BuildWorldState(state),
            DowntimeCompleted = state.DowntimeCompleted.Select(g => g.ToString()).ToArray(),
            WildCardAllianceStatus = state.WildCardAllianceStatus.ToString(),
            WildCardAllianceTurn = state.WildCardAllianceTurn,
            StepsSinceEncounter = state.StepsSinceEncounter,
            DungeonSeed = state.CurrentDungeon?.Seed ?? 0,
            IsIronman = state.IsIronman,
            FactionTimelineModifiers = new Dictionary<string, int>(state.Campaign.FactionTimelineModifiers),
            FiredEvents = state.Campaign.FiredEvents.ToArray(),
            UnlockedDungeons = state.Campaign.UnlockedDungeons.ToArray(),
            ReadDocuments = state.Campaign.ReadDocuments.ToArray(),
            AnnouncedFactionStates = state.Campaign.AnnouncedFactionStates.ToArray(),
            RescueExpedition = BuildRescueExpedition(state),
            BetrayalPath = state.Campaign.BetrayalPath,
            FamilyName = state.Campaign.FamilyName
        };
    }

    public static SavePartyMember?[] BuildParty(GameState state)
    {
        var party = new SavePartyMember?[6];
        for (int i = 0; i < 6; i++)
        {
            var m = state.Party.Members[i];
            if (m.Id != Guid.Empty)
            {
                party[i] = new SavePartyMember
                {
                    Id = m.Id,
                    Name = m.Name,
                    ClassId = m.ClassId,
                    Level = m.Level,
                    Xp = m.Xp,
                    BaseStats = m.BaseStats,
                    CurrentHp = m.CurrentHp,
                    Equipment = m.Equipment,
                    KnownAbilities = m.KnownAbilities,
                    Row = m.Row,
                    BranchChoice = m.BranchChoice,
                    BranchLevel6 = m.BranchLevel6,
                    TempModifiers = m.TempModifiers,
                    ComponentInventory = m.ComponentInventory.Select(BuildComponentStack).ToArray()
                };
            }
        }
        return party;
    }

    public static SavePlayer BuildPlayer(GameState state) => new()
    {
        X = state.Player.Position.X,
        Y = state.Player.Position.Y,
        Facing = state.Player.Facing.ToString()
    };

    public static SaveTownState BuildTown(GameState state) => new()
    {
        CurrentTownId = state.Town.CurrentTownId,
        AvailableMissions = state.Town.AvailableMissions
            .Select(m => new SaveMissionOffer
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                MinLevel = m.MinLevel,
                Rewards = m.Rewards,
                RepReward = m.RepReward,
                FactionId = m.FactionId
            }).ToArray(),
        VendorStock = state.Town.VendorStock.Select(BuildVendorItem).ToArray(),
        FactionVendors = state.Town.FactionVendors
            .Select(fv => new SaveFactionVendor
            {
                FactionId = fv.FactionId,
                Name = fv.Name,
                Threshold = fv.Threshold,
                Stock = fv.Stock.Select(BuildVendorItem).ToArray()
            }).ToArray(),
        FactionContacts = state.Town.FactionContacts
            .Select(c => new SaveFactionContact
            {
                Id = c.Id,
                Name = c.Name,
                FactionId = c.FactionId,
                Portrait = c.Portrait
            }).ToArray(),
        TavernRoster = state.Town.TavernRoster
            .Select(r => new SaveTavernRecruit
            {
                Id = r.Id,
                Name = r.Name,
                ClassId = r.ClassId,
                Level = r.Level,
                BaseStats = r.BaseStats,
                Cost = r.Cost
            }).ToArray(),
        ViewedMissions = state.Town.ViewedMissions.ToArray(),
        QuestLog = state.Town.QuestLog
            .Select(q => new SaveActiveMission
            {
                Id = q.Id,
                Title = q.Title,
                Description = q.Description,
                RepReward = q.RepReward,
                FactionId = q.FactionId,
                Status = q.Status.ToString()
            }).ToArray(),
        Rumors = state.Town.Rumors
            .Select(r => new SaveTownRumor
            {
                Id = r.Id,
                Text = r.Text,
                TruthStatus = r.TruthStatus.ToString(),
                Verified = r.Verified,
                VerificationResult = r.VerificationResult,
                RelatedContentId = r.RelatedContentId,
                RelatedFactionId = r.RelatedFactionId,
                HiddenTag = r.HiddenTag
            }).ToArray()
    };

    public static SaveActionLogEntry[] BuildActionLog(GameState state) =>
        state.ActionLog.Select(e => new SaveActionLogEntry
        {
            Turn = e.Turn,
            Act = e.Act,
            Category = e.Category,
            Type = e.Type,
            Payload = e.Payload
        }).ToArray();

    public static SaveComponentStack[] BuildExpeditionCache(GameState state) =>
        state.Party.ExpeditionCache.Select(BuildComponentStack).ToArray();

    public static SaveComponentStack[] BuildTownStorage(GameState state) =>
        state.Party.TownStorage.Select(BuildComponentStack).ToArray();

    public static SavePartyMember[] BuildBench(GameState state) =>
        state.Party.Bench.Select(b => new SavePartyMember
        {
            Id = b.Id,
            Name = b.Name,
            ClassId = b.ClassId,
            Level = b.Level,
            Xp = b.Xp,
            BaseStats = b.BaseStats,
            CurrentHp = b.CurrentHp,
            Equipment = b.Equipment,
            KnownAbilities = b.KnownAbilities,
            Row = b.Row,
            BranchChoice = b.BranchChoice,
            BranchLevel6 = b.BranchLevel6,
            TempModifiers = b.TempModifiers,
            ResurrectionAttempts = b.ResurrectionAttempts,
            BranchAdvancementLocked = b.BranchAdvancementLocked,
            ComponentInventory = b.ComponentInventory.Select(BuildComponentStack).ToArray()
        }).ToArray();

    public static SavePartyMember[] BuildDeadCharacters(GameState state) =>
        state.Party.DeadCharacters.Select(d => new SavePartyMember
        {
            Id = d.Id,
            Name = d.Name,
            ClassId = d.ClassId,
            Level = d.Level,
            Xp = d.Xp,
            BaseStats = d.BaseStats,
            CurrentHp = d.CurrentHp,
            Equipment = d.Equipment,
            KnownAbilities = d.KnownAbilities,
            Row = d.Row,
            BranchChoice = d.BranchChoice,
            BranchLevel6 = d.BranchLevel6,
            TempModifiers = d.TempModifiers,
            ResurrectionAttempts = d.ResurrectionAttempts,
            BranchAdvancementLocked = d.BranchAdvancementLocked
        }).ToArray();

    public static SaveRescueExpedition? BuildRescueExpedition(GameState state)
    {
        var rescue = state.RescueExpedition;
        if (rescue is null) return null;

        return new SaveRescueExpedition
        {
            IsActive = rescue.IsActive,
            RescuePartyIds = rescue.RescuePartyIds.Select(id => id.ToString()).ToArray(),
            DungeonType = rescue.DungeonType,
            TpkX = rescue.TpkLocation.X,
            TpkY = rescue.TpkLocation.Y,
            Success = rescue.Success,
            Resolved = rescue.Resolved
        };
    }

    public static SaveJournalState BuildJournal(GameState state) => new()
    {
        DiscoveredSynergies = state.Journal.DiscoveryOrder.ToArray()
    };

    public static SaveHeatState BuildHeat(GameState state) => new()
    {
        Value = state.Heat.Value
    };

    public static SaveCampaignConfig? BuildCampaignConfig(GameState state)
    {
        if (state.CampaignConfig == null) return null;
        return new SaveCampaignConfig
        {
            Patron = state.CampaignConfig.Patron,
            Threat = state.CampaignConfig.Threat,
            Mastermind = state.CampaignConfig.Mastermind,
            Scheme = state.CampaignConfig.Scheme.ToString(),
            WildCard = state.CampaignConfig.WildCard,
            Complication = state.CampaignConfig.Complication.ToString(),
            EvidenceChain = state.CampaignConfig.EvidenceChain.ToArray(),
            FactionTimelines = state.CampaignConfig.FactionTimelines.ToDictionary(
                kv => kv.Key,
                kv => new SaveFactionTimeline { Preparing = kv.Value.Preparing, Executing = kv.Value.Executing }),
            NpcCasting = new Dictionary<string, string>(state.CampaignConfig.NpcCasting),
            WildcardTrigger = state.CampaignConfig.WildcardTrigger == null ? null : new SaveWildcardTrigger
            {
                FactionId = state.CampaignConfig.WildcardTrigger.FactionId,
                TurnThreshold = state.CampaignConfig.WildcardTrigger.TurnThreshold
            }
        };
    }

    public static SaveOverworldNode[] BuildOverworldNodes(GameState state) =>
        state.Overworld.Nodes.Values.Select(n => new SaveOverworldNode
        {
            Id = n.Id,
            Name = n.Name,
            Type = n.Type.ToString(),
            FactionPresence = n.FactionPresence.ToArray(),
            DungeonTemplateId = n.DungeonTemplateId
        }).ToArray();

    public static SaveOverworldRoute[] BuildOverworldRoutes(GameState state) =>
        state.Overworld.Routes.Select(r => new SaveOverworldRoute
        {
            From = r.From,
            To = r.To,
            Distance = r.Distance,
            DangerRating = r.DangerRating,
            Terrain = r.Terrain,
            Status = r.Status.ToString()
        }).ToArray();

    public static SaveWorldState BuildWorldState(GameState state) => new()
    {
        Settlements = new Dictionary<string, string>(state.WorldState.Settlements),
        AccessibleDungeons = state.WorldState.AccessibleDungeons.ToArray(),
        FactionTerritory = state.WorldState.FactionTerritory.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToArray())
    };

    private static SaveComponentStack BuildComponentStack(ComponentStack c) => new()
    {
        ItemId = c.ItemId,
        Count = c.Count,
        MaxStack = c.MaxStack,
        DungeonTurnsAlive = c.DungeonTurnsAlive,
        Stabilized = c.Stabilized
    };

    private static SaveVendorItem BuildVendorItem(VendorItem v) => new()
    {
        ItemId = v.ItemId,
        Name = v.Name,
        Price = v.Price,
        Quantity = v.Quantity
    };
}
