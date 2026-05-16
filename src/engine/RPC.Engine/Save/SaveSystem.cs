using System.Text.Json;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Overworld;
using RPC.Engine.Save.Migrations;
using RPC.Engine.Town;

namespace RPC.Engine.Save;

public class SaveData
{
    public int SchemaVersion { get; set; }
    public string? ContentHash { get; set; }
    public SavePartyMember?[] Party { get; set; } = new SavePartyMember?[6];
    public SavePlayer Player { get; set; } = new();
    public string? DungeonType { get; set; }
    public string[] ExploredTiles { get; set; } = Array.Empty<string>();
    public string Mode { get; set; } = "Menu";
    public SaveTownState? Town { get; set; }
    public SaveActionLogEntry[] ActionLog { get; set; } = Array.Empty<SaveActionLogEntry>();
    public Dictionary<string, int> Reputation { get; set; } = new();
    public Dictionary<string, int> Evidence { get; set; } = new();
    public string? SuspectedFaction { get; set; }
    public string? Settings { get; set; }
    public int OverworldTurns { get; set; } = 0;
    public string OverworldCurrentNodeId { get; set; } = "the_reach";
    public bool CampaignEnded { get; set; } = false;
    public string? AccusedFaction { get; set; }
    public bool MastermindAdvantage { get; set; } = false;
    public bool FinalDungeonUnlocked { get; set; } = false;
    public int PartyGold { get; set; } = 500;
    public int TitheTokens { get; set; } = 0;
    public string[] PartyInventory { get; set; } = Array.Empty<string>();
    public SaveComponentStack[] ExpeditionCache { get; set; } = Array.Empty<SaveComponentStack>();
    public SavePartyMember[] DeadCharacters { get; set; } = Array.Empty<SavePartyMember>();
    public SaveJournalState? Journal { get; set; }
    public SaveHeatState? Heat { get; set; }
    public SaveCampaignConfig? CampaignConfig { get; set; }
    public SaveOverworldNode[] OverworldNodes { get; set; } = Array.Empty<SaveOverworldNode>();
    public SaveOverworldRoute[] OverworldRoutes { get; set; } = Array.Empty<SaveOverworldRoute>();
    public int CurrentAct { get; set; } = 1;
    public SaveWorldState? WorldState { get; set; }
    public string[] DowntimeCompleted { get; set; } = Array.Empty<string>();
    public string? WildCardAllianceStatus { get; set; }
    public int WildCardAllianceTurn { get; set; }
}

public class SaveFactionTimeline
{
    public int Preparing { get; set; }
    public int Executing { get; set; }
}

public class SaveWildcardTrigger
{
    public string FactionId { get; set; } = "";
    public int TurnThreshold { get; set; }
}

public class SaveCampaignConfig
{
    public string Patron { get; set; } = "";
    public string Threat { get; set; } = "";
    public string Mastermind { get; set; } = "";
    public string Scheme { get; set; } = "";
    public string WildCard { get; set; } = "";
    public string Complication { get; set; } = "";
    public string[] EvidenceChain { get; set; } = Array.Empty<string>();
    public Dictionary<string, SaveFactionTimeline> FactionTimelines { get; set; } = new();
    public Dictionary<string, string> NpcCasting { get; set; } = new();
    public SaveWildcardTrigger? WildcardTrigger { get; set; }
}

public class SaveJournalState
{
    public string[] DiscoveredSynergies { get; set; } = Array.Empty<string>();
}

public class SaveHeatState
{
    public int Value { get; set; }
}

public class SaveWorldState
{
    public Dictionary<string, string> Settlements { get; set; } = new();
    public string[] AccessibleDungeons { get; set; } = Array.Empty<string>();
    public Dictionary<string, string[]> FactionTerritory { get; set; } = new();
}

public class SaveTownState
{
    public string CurrentTownId { get; set; } = "the_reach";
    public SaveMissionOffer[] AvailableMissions { get; set; } = Array.Empty<SaveMissionOffer>();
    public SaveVendorItem[] VendorStock { get; set; } = Array.Empty<SaveVendorItem>();
    public SaveFactionVendor[] FactionVendors { get; set; } = Array.Empty<SaveFactionVendor>();
    public SaveFactionContact[] FactionContacts { get; set; } = Array.Empty<SaveFactionContact>();
    public SaveTavernRecruit[] TavernRoster { get; set; } = Array.Empty<SaveTavernRecruit>();
    public string[] ViewedMissions { get; set; } = Array.Empty<string>();
    public SaveActiveMission[] QuestLog { get; set; } = Array.Empty<SaveActiveMission>();
    public SaveTownRumor[] Rumors { get; set; } = Array.Empty<SaveTownRumor>();
}

public class SaveMissionOffer
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int MinLevel { get; set; }
    public string[] Rewards { get; set; } = Array.Empty<string>();
    public int RepReward { get; set; }
    public string FactionId { get; set; } = "";
}

public class SaveVendorItem
{
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Price { get; set; }
    public int Quantity { get; set; }
}

public class SaveFactionVendor
{
    public string FactionId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Threshold { get; set; }
    public SaveVendorItem[] Stock { get; set; } = Array.Empty<SaveVendorItem>();
}

public class SaveActionLogEntry
{
    public int Turn { get; set; }
    public string Category { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, string> Payload { get; set; } = new();
}

public class SaveTavernRecruit
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassId { get; set; } = "";
    public int Level { get; set; }
    public BaseStats BaseStats { get; set; }
    public int Cost { get; set; }
}

public class SaveFactionContact
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string Portrait { get; set; } = "";
}

public class SaveActiveMission
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int RepReward { get; set; }
    public string FactionId { get; set; } = "";
    public string Status { get; set; } = "";
}

public class SaveTownRumor
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string TruthStatus { get; set; } = "";
    public bool Verified { get; set; }
    public bool? VerificationResult { get; set; }
    public string? RelatedContentId { get; set; }
    public string? RelatedFactionId { get; set; }
}

public class SaveOverworldNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string[] FactionPresence { get; set; } = Array.Empty<string>();
    public string? DungeonTemplateId { get; set; }
}

public class SaveOverworldRoute
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Distance { get; set; }
    public int DangerRating { get; set; }
    public string Terrain { get; set; } = "";
    public string Status { get; set; } = "";
}

public class SaveComponentStack
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; }
    public int MaxStack { get; set; } = 99;
}

public class SavePartyMember
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ClassId { get; set; } = "";
    public int Level { get; set; }
    public int Xp { get; set; }
    public BaseStats BaseStats { get; set; }
    public int CurrentHp { get; set; }
    public Equipment Equipment { get; set; }
    public string[] KnownAbilities { get; set; } = Array.Empty<string>();
    public int Row { get; set; }
    public string? BranchChoice { get; set; }
    public string? BranchLevel6 { get; set; }
    public TempStatModifier[] TempModifiers { get; set; } = Array.Empty<TempStatModifier>();
    public int ResurrectionAttempts { get; set; } = 0;
    public bool BranchAdvancementLocked { get; set; } = false;
    public SaveComponentStack[] ComponentInventory { get; set; } = Array.Empty<SaveComponentStack>();
}

public class SavePlayer
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Facing { get; set; } = "North";
}

public static class SaveSystem
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string SavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheReach", "save.json");

    public static void Save(GameState state, string? path = null, string? contentHash = null)
    {
        var fileIo = new SaveFileIO(path);
        var data = BuildSaveData(state);
        data.ContentHash = contentHash;
        var json = fileIo.Serialize(data);
        fileIo.WriteAtomic(json);
    }

    public static bool Load(GameState state, string? path = null, string? expectedContentHash = null)
    {
        var fileIo = new SaveFileIO(path);
        if (!fileIo.Exists())
            return false;

        try
        {
            var json = fileIo.ReadAllText();
            if (json == null) return false;

            var doc = JsonDocument.Parse(json);
            var schemaVersion = doc.RootElement.TryGetProperty("schemaVersion", out var svProp)
                ? svProp.GetInt32()
                : 0;

            var pipeline = SaveMigrationPipeline.CreateDefault(8);
            if (!pipeline.CanMigrate(schemaVersion))
            {
                var quarantinePath = fileIo.Quarantine($"unsupported schema version {schemaVersion}");
                Console.Error.WriteLine(
                    $"Save file '{fileIo.SavePath}' has unsupported schema version {schemaVersion}. Quarantined to '{quarantinePath}'.");
                return false;
            }

            var migrated = pipeline.Migrate(doc, schemaVersion);
            var data = JsonSerializer.Deserialize<SaveData>(migrated, Options);
            if (data == null) return false;

            if (!string.IsNullOrEmpty(expectedContentHash) && data.ContentHash != expectedContentHash)
            {
                Console.WriteLine(
                    $"[Save] Content hash mismatch: save was created with '{data.ContentHash ?? "(none)"}', current is '{expectedContentHash}'. Loading anyway.");
            }

            RestoreParty(state, data);
            RestorePlayer(state, data);
            RestoreExploredTiles(state, data);
            RestoreMode(state, data);
            RestoreDungeonType(state, data);
            RestoreTown(state, data);
            RestoreActionLog(state, data);
            RestoreReputation(state, data);
            RestoreOverworld(state, data);
            RestoreSettings(state, data);
            RestoreJournal(state, data);
            RestoreHeat(state, data);
            RestoreCampaignConfig(state, data);
            RestoreEvidence(state, data);
            RestoreWorldState(state, data);
            RestoreDowntime(state, data);
            RestoreWildCardAlliance(state, data);

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load save: {ex.Message}");
            return false;
        }
    }

    public static bool HasSave(string? path = null)
    {
        return new SaveFileIO(path).Exists();
    }

    private static SaveData BuildSaveData(GameState state)
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
                    ComponentInventory = m.ComponentInventory.Select(c => new SaveComponentStack
                    {
                        ItemId = c.ItemId,
                        Count = c.Count,
                        MaxStack = c.MaxStack
                    }).ToArray()
                };
            }
        }

        return new SaveData
        {
            SchemaVersion = 8,
            Party = party,
            Player = new SavePlayer
            {
                X = state.Player.Position.X,
                Y = state.Player.Position.Y,
                Facing = state.Player.Facing.ToString()
            },
            DungeonType = state.CurrentDungeonType,
            ExploredTiles = state.ExploredTiles.AsEnumerable().ToArray(),
            Mode = state.Mode.ToString(),
            Town = new SaveTownState
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
                VendorStock = state.Town.VendorStock
                    .Select(v => new SaveVendorItem
                    {
                        ItemId = v.ItemId,
                        Name = v.Name,
                        Price = v.Price,
                        Quantity = v.Quantity
                    }).ToArray(),
                FactionVendors = state.Town.FactionVendors
                    .Select(fv => new SaveFactionVendor
                    {
                        FactionId = fv.FactionId,
                        Name = fv.Name,
                        Threshold = fv.Threshold,
                        Stock = fv.Stock
                            .Select(v => new SaveVendorItem
                            {
                                ItemId = v.ItemId,
                                Name = v.Name,
                                Price = v.Price,
                                Quantity = v.Quantity
                            }).ToArray()
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
                        RelatedFactionId = r.RelatedFactionId
                    }).ToArray()
            },
            ActionLog = state.ActionLog.Select(e => new SaveActionLogEntry
            {
                Turn = e.Turn,
                Category = e.Category,
                Type = e.Type,
                Payload = e.Payload
            }).ToArray(),
            Reputation = new Dictionary<string, int>(state.Reputation),
            Evidence = new Dictionary<string, int>(state.Evidence.Counters),
            SuspectedFaction = state.Evidence.SuspectedFaction,
            Settings = state.SettingsHash,
            PartyGold = state.PartyGold,
            TitheTokens = state.TitheTokens,
            PartyInventory = state.PartyInventory.ToArray(),
            ExpeditionCache = state.Party.ExpeditionCache.Select(c => new SaveComponentStack
            {
                ItemId = c.ItemId,
                Count = c.Count,
                MaxStack = c.MaxStack
            }).ToArray(),
            DeadCharacters = state.Party.DeadCharacters.Select(d => new SavePartyMember
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
            }).ToArray(),
            OverworldTurns = state.Overworld.Turns,
            OverworldCurrentNodeId = state.Overworld.CurrentNodeId,
            CampaignEnded = state.CampaignEnded,
            AccusedFaction = state.AccusedFaction,
            MastermindAdvantage = state.MastermindAdvantage,
            FinalDungeonUnlocked = state.FinalDungeonUnlocked,
            Journal = new SaveJournalState
            {
                DiscoveredSynergies = state.Journal.DiscoveryOrder.ToArray()
            },
            Heat = new SaveHeatState
            {
                Value = state.Heat.Value
            },
            CampaignConfig = state.CampaignConfig == null ? null : new SaveCampaignConfig
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
            },
            OverworldNodes = state.Overworld.Nodes.Values.Select(n => new SaveOverworldNode
            {
                Id = n.Id,
                Name = n.Name,
                Type = n.Type.ToString(),
                FactionPresence = n.FactionPresence.ToArray(),
                DungeonTemplateId = n.DungeonTemplateId
            }).ToArray(),
            OverworldRoutes = state.Overworld.Routes.Select(r => new SaveOverworldRoute
            {
                From = r.From,
                To = r.To,
                Distance = r.Distance,
                DangerRating = r.DangerRating,
                Terrain = r.Terrain,
                Status = r.Status.ToString()
            }).ToArray(),
            CurrentAct = state.CurrentAct,
            WorldState = new SaveWorldState
            {
                Settlements = new Dictionary<string, string>(state.WorldState.Settlements),
                AccessibleDungeons = state.WorldState.AccessibleDungeons.ToArray(),
                FactionTerritory = state.WorldState.FactionTerritory.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ToArray())
            },
            DowntimeCompleted = state.DowntimeCompleted.Select(g => g.ToString()).ToArray(),
            WildCardAllianceStatus = state.WildCardAllianceStatus.ToString(),
            WildCardAllianceTurn = state.WildCardAllianceTurn
        };
    }

    private static void RestoreParty(GameState state, SaveData data)
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

    private static void RestorePlayer(GameState state, SaveData data)
    {
        if (Enum.TryParse<Direction>(data.Player.Facing, out var facing))
        {
            state.Player = new Player(
                new Position(data.Player.X, data.Player.Y),
                facing);
        }
    }

    private static void RestoreExploredTiles(GameState state, SaveData data)
    {
        state.ExploredTiles.Clear();
        foreach (var tile in data.ExploredTiles)
            state.ExploredTiles.Add(tile);
    }

    private static void RestoreMode(GameState state, SaveData data)
    {
        if (Enum.TryParse<GameMode>(data.Mode, out var mode))
            state.Mode = mode;
    }

    private static void RestoreTown(GameState state, SaveData data)
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

    private static void RestoreActionLog(GameState state, SaveData data)
    {
        state.RestoreActionLog(
            (data.ActionLog ?? Array.Empty<SaveActionLogEntry>())
            .Select(e => new ActionLogEntry(e.Turn, e.Category, e.Type, e.Payload))
            .ToList());
    }

    private static void RestoreDungeonType(GameState state, SaveData data)
    {
        state.CurrentDungeonType = data.DungeonType;
    }

    private static void RestoreReputation(GameState state, SaveData data)
    {
        state.Reputation.Clear();
        foreach (var kv in data.Reputation)
        {
            state.Reputation[kv.Key] = Math.Clamp(kv.Value, -100, 100);
        }
    }

    private static void RestoreSettings(GameState state, SaveData data)
    {
        state.SettingsHash = data.Settings;
    }

    private static void RestoreJournal(GameState state, SaveData data)
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

    private static void RestoreHeat(GameState state, SaveData data)
    {
        if (data.Heat != null)
        {
            state.Heat.Value = data.Heat.Value;
        }
    }

    private static void RestoreCampaignConfig(GameState state, SaveData data)
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

    private static void RestoreOverworld(GameState state, SaveData data)
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

    private static void RestoreEvidence(GameState state, SaveData data)
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

    private static void RestoreWorldState(GameState state, SaveData data)
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

    private static void RestoreDowntime(GameState state, SaveData data)
    {
        var ids = (data.DowntimeCompleted ?? Array.Empty<string>())
            .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();
        state.RestoreDowntimeState(ids);
    }

    private static void RestoreWildCardAlliance(GameState state, SaveData data)
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
}
