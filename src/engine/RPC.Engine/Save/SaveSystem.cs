using System.Text.Json;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Overworld;
using RPC.Engine.Save.Migrations;
using RPC.Engine.Town;

namespace RPC.Engine.Save;

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

    public static bool Load(GameState state, string? path = null, string? expectedContentHash = null, Func<string, int?, Dungeon>? dungeonGenerator = null)
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

            SaveRestorer.RestoreParty(state, data);
            SaveRestorer.RestorePlayer(state, data);
            SaveRestorer.RestoreExploredTiles(state, data);
            SaveRestorer.RestoreMode(state, data);
            SaveRestorer.RestoreDungeonType(state, data);
            SaveRestorer.RestoreTown(state, data);
            SaveRestorer.RestoreActionLog(state, data);
            SaveRestorer.RestoreReputation(state, data);
            SaveRestorer.RestoreOverworld(state, data);
            SaveRestorer.RestoreSettings(state, data);
            SaveRestorer.RestoreJournal(state, data);
            SaveRestorer.RestoreHeat(state, data);
            SaveRestorer.RestoreCampaignConfig(state, data);
            SaveRestorer.RestoreEvidence(state, data);
            SaveRestorer.RestoreWorldState(state, data);
            SaveRestorer.RestoreDowntime(state, data);
            SaveRestorer.RestoreWildCardAlliance(state, data);
            SaveRestorer.RestoreStepsSinceEncounter(state, data);
            SaveRestorer.RestoreIronman(state, data);

            if (dungeonGenerator != null
                && state.Mode == GameMode.Exploration
                && state.CurrentDungeon == null
                && !string.IsNullOrEmpty(state.CurrentDungeonType))
            {
                state.CurrentDungeon = dungeonGenerator(state.CurrentDungeonType, data.DungeonSeed != 0 ? data.DungeonSeed : null);
            }

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
            WildCardAllianceTurn = state.WildCardAllianceTurn,
            StepsSinceEncounter = state.StepsSinceEncounter,
            DungeonSeed = state.CurrentDungeon?.Seed ?? 0,
            IsIronman = state.IsIronman,
            FactionTimelineModifiers = new Dictionary<string, int>(state.Campaign.FactionTimelineModifiers),
            FiredEvents = state.Campaign.FiredEvents.ToArray(),
            UnlockedDungeons = state.Campaign.UnlockedDungeons.ToArray(),
            BetrayalPath = state.Campaign.BetrayalPath
        };
    }
}
