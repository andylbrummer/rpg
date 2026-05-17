using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Exploration;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Dungeons;
using RPC.Engine.Overworld;
using RPC.Engine.Party;
using RPC.Engine.Save;
using RPC.Engine.Town;
using RPC.Engine.Travel;

namespace RPC.Engine;

public class GameState
{
    // Feature state aggregates
    public ExplorationState Exploration { get; } = new();
    public CampaignState Campaign { get; } = new();

    // Exploration forwarding properties
    public Player Player { get => Exploration.Player; set => Exploration.Player = value; }
    public Dungeon? CurrentDungeon { get => Exploration.CurrentDungeon; set => Exploration.CurrentDungeon = value; }
    public string? CurrentDungeonType { get => Exploration.CurrentDungeonType; set => Exploration.CurrentDungeonType = value; }
    public BoundedTileSet ExploredTiles => Exploration.ExploredTiles;
    public int StepsSinceEncounter { get => Exploration.StepsSinceEncounter; set => Exploration.StepsSinceEncounter = value; }
    internal string? CurrentEncounterId { get => Exploration.CurrentEncounterId; set => Exploration.CurrentEncounterId = value; }
    internal Position? PendingTaggedEncounterTile { get => Exploration.PendingTaggedEncounterTile; set => Exploration.PendingTaggedEncounterTile = value; }

    // Campaign forwarding properties
    public ReputationState Reputation => Campaign.Reputation;
    public EvidenceState Evidence => Campaign.Evidence;
    public HeatState Heat => Campaign.Heat;
    public JournalState Journal => Campaign.Journal;
    public WorldState WorldState { get => Campaign.WorldState; set => Campaign.WorldState = value; }
    public CampaignConfig? CampaignConfig { get => Campaign.CampaignConfig; set => Campaign.CampaignConfig = value; }
    public SchemeDef? CurrentScheme { get => Campaign.CurrentScheme; set => Campaign.CurrentScheme = value; }
    public ComplicationDef? CurrentComplication { get => Campaign.CurrentComplication; set => Campaign.CurrentComplication = value; }
    public bool CampaignEnded { get => Campaign.CampaignEnded; set => Campaign.CampaignEnded = value; }
    public string? AccusedFaction { get => Campaign.AccusedFaction; set => Campaign.AccusedFaction = value; }
    public bool MastermindAdvantage { get => Campaign.MastermindAdvantage; set => Campaign.MastermindAdvantage = value; }
    public bool FinalDungeonUnlocked { get => Campaign.FinalDungeonUnlocked; set => Campaign.FinalDungeonUnlocked = value; }
    public WildCardAllianceStatus WildCardAllianceStatus { get => Campaign.WildCardAllianceStatus; set => Campaign.WildCardAllianceStatus = value; }
    public int WildCardAllianceTurn { get => Campaign.WildCardAllianceTurn; set => Campaign.WildCardAllianceTurn = value; }

    // Cross-cutting state (remains on GameState as coordinator)
    public GameMode Mode { get; set; } = GameMode.Exploration;
    public DateTime LastUpdate { get; set; }
    public PartyState Party { get; set; } = new();
    public TownState Town { get; set; } = new();
    public OverworldState Overworld { get; set; } = new();
    public CombatState? Combat { get; internal set; }
    public CombatResult? LastCombatResult { get; internal set; }
    public List<CombatLogEntry> CombatLog => Combat?.Log ?? new List<CombatLogEntry>();
    public List<ActionLogEntry> ActionLog { get; } = new();
    public int PartyGold { get; set; } = 500;
    public int TitheTokens { get; set; } = 0;
    public List<string> PartyInventory { get; set; } = new();
    public int CurrentAct => Overworld.Turns <= 15 ? 1 : Overworld.Turns <= 25 ? 2 : 3;
    public TravelEncounterState? CurrentTravelEncounter { get; internal set; }
    public RescueExpeditionState? RescueExpedition { get; set; }
    public int RolledTravelEncounterCount { get; internal set; }
    public int ResolvedTravelEncounterCount { get; internal set; }
    public ParleyOffer? CurrentParley { get; internal set; }
    public IReadOnlySet<Guid> DowntimeCompleted => _downtimeCompleted;
    public string? SettingsHash { get; set; }
    public bool IsIronman { get; set; } = false;
    public string SavePath { get; set; } = SaveSystem.SavePath;

    public void ClearCombatResult()
    {
        LastCombatResult = null;
    }

    internal readonly GameRandom _encounterRng;
    private readonly EncounterTableRegistry? _encounterTables;
    private readonly ClassRegistry? _classRegistry;
    private readonly int _seed;
    private int _actionLogTurn = 0;
    internal readonly HashSet<Guid> _downtimeCompleted = new();

    private readonly CombatService _combatService;
    private readonly ExplorationService _explorationService;
    private readonly TownService _townService;
    private readonly OverworldService _overworldService;
    private readonly CampaignService _campaignService;
    private readonly MissionService _missionService;
    private readonly EventScheduler _eventScheduler;
    private readonly FactionInteractionService _factionInteractionService;
    private readonly IReadOnlyDictionary<string, DungeonTemplate> _dungeonTemplates;
    public Analytics.AnalyticsTracker Analytics { get; } = new();

    public GameState(int? seed = null, EncounterTableRegistry? encounterTables = null, ClassRegistry? classRegistry = null, SynergyRegistry? synergies = null, FactionContentRepository? factionContent = null, RumorRepository? rumors = null, IReadOnlyDictionary<string, DungeonTemplate>? dungeonTemplates = null)
    {
        LastUpdate = DateTime.UtcNow;
        _seed = seed ?? DateTime.UtcNow.GetHashCode();
        _encounterRng = new GameRandom(_seed);
        _encounterTables = encounterTables;
        _classRegistry = classRegistry;
        _townService = new TownService(factionContent, rumors);
        InitializeDefaultParty();
        InitializeTown();
        Overworld = new OverworldState();
        Mode = GameMode.Menu; // Start in town/hub

        _combatService = new CombatService(_encounterTables, _classRegistry, _encounterRng, synergies);
        _explorationService = new ExplorationService(_encounterTables, _classRegistry, _encounterRng);
        _overworldService = new OverworldService(_encounterRng, _classRegistry, synergies);
        _campaignService = new CampaignService(_classRegistry);
        _missionService = new MissionService(_classRegistry);
        _eventScheduler = new EventScheduler(_campaignService);
        _factionInteractionService = new FactionInteractionService(_campaignService);
        _dungeonTemplates = dungeonTemplates ?? new Dictionary<string, DungeonTemplate>();
        Analytics = new Analytics.AnalyticsTracker();
    }

    private void InitializeDefaultParty()
    {
        Party.SetMember(0, new CharacterState(
            new Guid("11111111-1111-1111-1111-111111111111"), "Kael", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            new[] { "bone_spear", "tithe_touch" }, 0, null, null, null, 0, false, Array.Empty<ComponentStack>()));
        Party.SetMember(1, new CharacterState(
            new Guid("22222222-2222-2222-2222-222222222222"), "Sera", "stillblade", 1, 0,
            new BaseStats(5, 5, 4, 3, 4), 14, Equipment.Empty,
            new[] { "rend", "silence_strike" }, 0, null, null, null, 0, false, Array.Empty<ComponentStack>()));
        Party.SetMember(2, new CharacterState(
            new Guid("33333333-3333-3333-3333-333333333333"), "Mira", "cauterist", 1, 0,
            new BaseStats(3, 5, 4, 5, 4), 14, Equipment.Empty,
            new[] { "cauterize", "scalpel_dance" }, 0, null, null, null, 0, false, Array.Empty<ComponentStack>()));
        Party.SetMember(3, new CharacterState(
            new Guid("44444444-4444-4444-4444-444444444444"), "Vex", "hollow", 1, 0,
            new BaseStats(4, 6, 3, 4, 4), 11, Equipment.Empty,
            new[] { "shiv", "smoke_bomb" }, 1, null, null, null, 0, false, Array.Empty<ComponentStack>()));
        Party.SetMember(4, new CharacterState(
            new Guid("55555555-5555-5555-5555-555555555555"), "Nyx", "stillblade", 1, 0,
            new BaseStats(5, 5, 4, 3, 4), 14, Equipment.Empty,
            new[] { "rend", "silence_strike" }, 1, null, null, null, 0, false, Array.Empty<ComponentStack>()));
        Party.SetMember(5, new CharacterState(
            new Guid("66666666-6666-6666-6666-666666666666"), "Orin", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            new[] { "bone_spear", "tithe_touch" }, 1, null, null, null, 0, false, Array.Empty<ComponentStack>()));
    }

    private void InitializeTown()
    {
        if (Town.TavernRoster.Count == 0)
        {
            Town.TavernRoster = TavernRecruitGenerator.GenerateRoster(_seed);
        }
        if (Town.FactionContacts.Count == 0)
        {
            Town.FactionContacts = _townService.GenerateContacts();
        }
        if (Town.AvailableMissions.Count == 0)
        {
            Town.AvailableMissions = _townService.GenerateMissions();
        }
        if (Town.FactionVendors.Count == 0)
        {
            Town.FactionVendors = _townService.GenerateVendors();
        }
        if (string.IsNullOrEmpty(Town.CurrentTownId))
        {
            Town.CurrentTownId = "the_reach";
        }
    }

    public bool HasPendingBranchChoices => Party.Members.Any(m => m.Id != Guid.Empty && m.AwaitingBranchChoice);

    public bool HasPendingLevel6BranchChoices => Party.Members.Any(m => m.Id != Guid.Empty && m.Level >= 6 && m.BranchChoice != null && m.BranchLevel6 == null);

    public void EnterDungeon(Dungeon dungeon, string dungeonType)
    {
        _explorationService.EnterDungeon(this, dungeon, dungeonType);
    }

    public void ExploreAroundPlayer()
    {
        _explorationService.ExploreAroundPlayer(this);
    }

    public bool TryMoveForward() => _explorationService.TryMoveForward(this);
    public bool TryMoveBack() => _explorationService.TryMoveBack(this);
    public bool TryStrafeLeft() => _explorationService.TryStrafeLeft(this);
    public bool TryStrafeRight() => _explorationService.TryStrafeRight(this);

    public void TurnLeft() => _explorationService.TurnLeft(this);
    public void TurnRight() => _explorationService.TurnRight(this);

    public void TriggerEncounter(EncounterDef? encounter = null)
    {
        _combatService.TriggerEncounter(this, encounter);
    }

    public bool ResolveParley(string choice)
    {
        return _combatService.ResolveParley(this, choice);
    }

    public bool SubmitCombatAction(CombatAction action)
    {
        return _combatService.SubmitCombatAction(this, action);
    }

    public void FleeCombat()
    {
        _combatService.FleeCombat(this);
    }

    public void RestAtInn() => _townService.RestAtInn(this);

    public DowntimeResult? PerformDowntimeAction(Guid characterId, DowntimeAction action)
    {
        return _townService.PerformDowntimeAction(this, characterId, action);
    }

    public void RestoreDowntimeState(IEnumerable<Guid> ids)
    {
        _downtimeCompleted.Clear();
        foreach (var id in ids)
            _downtimeCompleted.Add(id);
    }

    public void ReturnToTown() => _townService.ReturnToTown(this);

    public void GenerateOverworld(CampaignConfig config) => _overworldService.GenerateOverworld(this, config);

    public FactionState GetFactionState(string factionId) => _campaignService.GetFactionState(this, factionId);

    public bool CheckWildCardTrigger() => _campaignService.CheckWildCardTrigger(this);

    public bool AcceptWildCardAlliance() => _campaignService.AcceptWildCardAlliance(this);

    public bool RefuseWildCardAlliance() => _campaignService.RefuseWildCardAlliance(this);

    public bool IgnoreWildCardAlliance() => _campaignService.IgnoreWildCardAlliance(this);

    public bool IsWildCardAllianceActive => WildCardAllianceStatus == WildCardAllianceStatus.Accepted;

    public string? WildCardFactionId => CampaignConfig?.WildcardTrigger?.FactionId;

    public bool Travel(string targetId) => _overworldService.Travel(this, targetId);

    public void IncrementTurns(int amount, int? dangerOverride = null)
    {
        _overworldService.IncrementTurns(this, amount, dangerOverride);
        if (Mode != GameMode.Menu)
        {
            _eventScheduler.Tick(this);
            _factionInteractionService.CheckAndResolveInteractions(this);
        }
    }

    public bool ResolveTravelEncounter(string choice) => _overworldService.ResolveTravelEncounter(this, choice);

    public void Reset()
    {
        Mode = GameMode.Menu;
        Combat = null;
        LastCombatResult = null;
        Player = new Player(new Position(32, 32), Direction.North);
        Exploration.Reset();
        Town = new TownState();
        Overworld = new OverworldState();
        Campaign.Reset();
        PartyGold = 500;
        TitheTokens = 0;
        PartyInventory.Clear();
        Party.DeadCharacters.Clear();
        InitializeDefaultParty();
        InitializeTown();
        ActionLog.Clear();
        _actionLogTurn = 0;
        CurrentTravelEncounter = null;
        RolledTravelEncounterCount = 0;
        ResolvedTravelEncounterCount = 0;
        LastUpdate = DateTime.UtcNow;
    }

    internal void ClearTaggedEncounterTile(bool resolved)
    {
        if (PendingTaggedEncounterTile.HasValue && CurrentDungeon != null && resolved)
        {
            var pos = PendingTaggedEncounterTile.Value;
            if (CurrentDungeon.IsValidPosition(pos))
            {
                var tile = CurrentDungeon.Tiles[pos.X, pos.Y];
                CurrentDungeon.Tiles[pos.X, pos.Y] = tile with { EncounterId = null };
            }
        }
        PendingTaggedEncounterTile = null;
    }

    internal void EmitActionLog(string category, string type, Dictionary<string, string> payload)
    {
        _actionLogTurn++;
        ActionLog.Add(new ActionLogEntry(_actionLogTurn, category, type, new Dictionary<string, string>(payload)));
        if (ActionLog.Count >= 1000)
        {
            Console.Error.WriteLine($"[DEV] ActionLog size warning: {ActionLog.Count} events. Consider log rotation.");
        }
    }

    public void RestoreActionLog(List<ActionLogEntry> entries)
    {
        ActionLog.Clear();
        ActionLog.AddRange(entries);
        _actionLogTurn = entries.Count > 0 ? entries.Max(e => e.Turn) : 0;
    }

    public void DiscoverSecret(string secretType, string secretId) => _campaignService.DiscoverSecret(this, secretType, secretId);

    public void ChooseSettlementFate(string settlementId, string fate) => _campaignService.ChooseSettlementFate(this, settlementId, fate);

    public string? ContentHash { get; set; }

    public void SaveGame(string? path = null) => Save.SaveSystem.Save(this, path, ContentHash);

    public void ApplyReputationDelta(string factionId, int delta, string source)
    {
        _campaignService.ApplyReputationDelta(this, factionId, delta, source);
        _campaignService.CheckOptionalDungeons(this, _dungeonTemplates);
    }

    public void AddEvidence(string factionId, string source, int amount = 1) => _campaignService.AddEvidence(this, factionId, source, amount);

    public int GetEvidenceThreshold(string factionId) => Evidence.GetThreshold(factionId);

    public bool AccuseFaction(string factionId) => _campaignService.AccuseFaction(this, factionId);

    public bool UnlockFinalDungeon() => _campaignService.UnlockFinalDungeon(this);

    public void SetAccusedFaction(string? factionId)
    {
        AccusedFaction = factionId;
    }

    public void SetMastermindAdvantage(bool value)
    {
        MastermindAdvantage = value;
    }

    public void SetFinalDungeonUnlocked(bool value)
    {
        FinalDungeonUnlocked = value;
    }

    public bool LoadGame(string? path = null, Func<string, int?, Dungeon>? dungeonGenerator = null) => Save.SaveSystem.Load(this, path, ContentHash, dungeonGenerator);

    public bool ChooseBranch(Guid characterId, string branch) => _campaignService.ChooseBranch(this, characterId, branch);

    public bool RecruitFromTavern(string recruitId) => _townService.RecruitFromTavern(this, recruitId);

    public ResurrectionResult? ResurrectCharacter(Guid characterId) => _townService.ResurrectCharacter(this, characterId);

    public bool AcceptMission(string missionId) => _missionService.AcceptMission(this, missionId);

    public bool CompleteMission(string missionId) => _missionService.CompleteMission(this, missionId);

    public bool FailMission(string missionId) => _missionService.FailMission(this, missionId);

    public bool AbandonMission(string missionId) => _missionService.AbandonMission(this, missionId);

    public bool ApplyDialogueReputation(string factionId, int delta) => _campaignService.ApplyDialogueReputation(this, factionId, delta);

    public void SetReputation(string factionId, int value)
    {
        _campaignService.SetReputation(this, factionId, value);
        _campaignService.CheckOptionalDungeons(this, _dungeonTemplates);
    }

    public bool PurchaseVendorItem(string itemId) => _townService.PurchaseVendorItem(this, itemId);

    public bool VerifyRumor(string rumorId, RumorVerificationSource source) => _townService.VerifyRumor(this, rumorId, source);

    public bool ChooseBetrayal() => _campaignService.ChooseBetrayal(this);

    public bool IsFragileState => IsIronman && Party.Bench.Count < 3 && Overworld.Turns > 25;

    public bool StartRescueExpedition()
    {
        if (!IsIronman || RescueExpedition?.IsActive == true)
            return false;

        var bench = Party.Bench.Where(c => c.IsAlive).Take(3).ToArray();
        if (bench.Length < 3)
            return false;

        RescueExpedition = new RescueExpeditionState
        {
            IsActive = true,
            RescuePartyIds = bench.Select(c => c.Id).ToArray(),
            DungeonType = CurrentDungeonType ?? "",
            TpkLocation = Player.Position
        };

        // Form rescue party from bench
        for (int i = 0; i < Party.Members.Length; i++)
            Party.SetMember(i, default);
        for (int i = 0; i < bench.Length && i < Party.Members.Length; i++)
            Party.SetMember(i, bench[i]);

        // Remove rescued characters from bench
        foreach (var r in bench)
            Party.Bench.Remove(r);

        // Reset rescue party to dungeon entrance
        if (CurrentDungeon != null)
        {
            for (int x = 0; x < CurrentDungeon.Width; x++)
            {
                for (int y = 0; y < CurrentDungeon.Height; y++)
                {
                    if (CurrentDungeon.Tiles[x, y].Type == TileType.Floor)
                    {
                        Player.Position = new Position(x, y);
                        Player.Facing = Direction.North;
                        ExploreAroundPlayer();
                        break;
                    }
                }
                if (Player.Position.X != RescueExpedition.TpkLocation.X || Player.Position.Y != RescueExpedition.TpkLocation.Y)
                    break;
            }
        }
        StepsSinceEncounter = 0;

        EmitActionLog("meta", "rescue_started", new Dictionary<string, string>
        {
            { "dungeonType", RescueExpedition.DungeonType },
            { "rescuers", string.Join(",", bench.Select(c => c.Name)) }
        });
        return true;
    }

    public void ResolveRescueExpedition(bool success)
    {
        if (RescueExpedition == null || !RescueExpedition.IsActive)
            return;

        RescueExpedition.Success = success;
        RescueExpedition.Resolved = true;
        RescueExpedition.IsActive = false;

        if (success)
        {
            // Recover equipment from dead characters to expedition cache
            var recovered = new List<ComponentStack>();
            foreach (var dead in Party.DeadCharacters.ToList())
            {
                foreach (var item in dead.ComponentInventory)
                {
                    recovered.Add(item);
                }
                EmitActionLog("meta", "equipment_recovered", new Dictionary<string, string>
                {
                    { "characterId", dead.Id.ToString() },
                    { "characterName", dead.Name }
                });
            }
            if (recovered.Count > 0)
            {
                Party.ExpeditionCache = Party.ExpeditionCache
                    .Concat(recovered)
                    .GroupBy(c => c.ItemId)
                    .Select(g => new ComponentStack(g.Key, g.Sum(c => c.Count), g.First().MaxStack))
                    .ToArray();
            }
            EmitActionLog("meta", "rescue_succeeded", new Dictionary<string, string> { { "dungeonType", RescueExpedition.DungeonType } });
            Mode = GameMode.Menu;
            CurrentDungeon = null;
            CurrentDungeonType = null;
            Exploration.Reset();
        }
        else
        {
            // Delete ironman save on rescue failure
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                    EmitActionLog("meta", "ironman_tpk", new Dictionary<string, string> { { "rescueFailed", "true" } });
                }
            }
            catch { }
        }
    }
}
