using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Dungeons;
using RPC.Engine.Overworld;
using RPC.Engine.Party;
using RPC.Engine.Services;
using RPC.Engine.Town;
using RPC.Engine.Travel;

namespace RPC.Engine;

public record ActionLogEntry(int Turn, string Category, string Type, Dictionary<string, string> Payload);
public record ParleyOffer(string EncounterId, string FactionId, string[] Options);
public record ResurrectionResult(
    bool Success,
    string? Error = null,
    int GoldCost = 0,
    int TitheTokenCost = 0,
    int StatLossCount = 0,
    bool BranchLocked = false,
    CharacterState? Character = null);

public class JournalState
{
    public List<string> DiscoveryOrder { get; } = new();
    public HashSet<string> Discovered { get; } = new();

    public void Discover(string id)
    {
        if (Discovered.Add(id))
            DiscoveryOrder.Add(id);
    }

    public bool IsDiscovered(string id) => Discovered.Contains(id);
}

public enum FactionState
{
    Investigating,
    Preparing,
    Executing
}

public enum WildCardAllianceStatus
{
    None,
    Offered,
    Accepted,
    Refused,
    Ignored
}

public class WorldState
{
    public Dictionary<string, string> Settlements { get; set; } = new();
    public List<string> AccessibleDungeons { get; set; } = new();
    public Dictionary<string, List<string>> FactionTerritory { get; set; } = new();

    public void Reset()
    {
        Settlements.Clear();
        AccessibleDungeons.Clear();
        FactionTerritory.Clear();
    }
}

public class GameState
{
    public Player Player { get; set; }
    public Dungeon? CurrentDungeon { get; set; }
    public GameMode Mode { get; set; } = GameMode.Exploration;
    public DateTime LastUpdate { get; set; }
    public PartyState Party { get; set; } = new();
    public TownState Town { get; set; } = new();
    public OverworldState Overworld { get; set; } = new();
    public CombatState? Combat { get; internal set; }
    public CombatResult? LastCombatResult { get; internal set; }
    public List<CombatLogEntry> CombatLog => Combat?.Log ?? new List<CombatLogEntry>();
    public List<ActionLogEntry> ActionLog { get; } = new();
    public ReputationState Reputation { get; } = new();
    public EvidenceState Evidence { get; } = new();
    public JournalState Journal { get; } = new();
    public HeatState Heat { get; } = new();
    public int PartyGold { get; set; } = 500;
    public int TitheTokens { get; set; } = 0;
    public List<string> PartyInventory { get; set; } = new();
    public bool CampaignEnded { get; set; } = false;
    public CampaignConfig? CampaignConfig { get; set; }
    public SchemeDef? CurrentScheme { get; set; }
    public ComplicationDef? CurrentComplication { get; set; }
    public WorldState WorldState { get; set; } = new();
    public int CurrentAct => Overworld.Turns <= 15 ? 1 : Overworld.Turns <= 25 ? 2 : 3;
    public string? AccusedFaction { get; internal set; }
    public bool MastermindAdvantage { get; internal set; }
    public bool FinalDungeonUnlocked { get; internal set; }
    public string? SettingsHash { get; set; }
    public TravelEncounterState? CurrentTravelEncounter { get; internal set; }
    public int RolledTravelEncounterCount { get; internal set; }
    public int ResolvedTravelEncounterCount { get; internal set; }
    public ParleyOffer? CurrentParley { get; internal set; }
    public IReadOnlySet<Guid> DowntimeCompleted => _downtimeCompleted;
    public WildCardAllianceStatus WildCardAllianceStatus { get; set; } = WildCardAllianceStatus.None;
    public int WildCardAllianceTurn { get; set; } = 0;

    public void ClearCombatResult()
    {
        LastCombatResult = null;
    }

    internal readonly GameRandom _encounterRng;
    private readonly EncounterTableRegistry? _encounterTables;
    private readonly ClassRegistry? _classRegistry;
    private readonly int _seed;
    internal int _stepsSinceEncounter = 0;
    private int _actionLogTurn = 0;
    internal string? _currentEncounterId;
    internal Position? _pendingTaggedEncounterTile;
    internal readonly HashSet<Guid> _downtimeCompleted = new();

    private readonly CombatService _combatService;
    private readonly ExplorationService _explorationService;
    private readonly TownService _townService;
    private readonly OverworldService _overworldService;
    private readonly CampaignService _campaignService;
    private readonly MissionService _missionService;

    public GameState(int? seed = null, EncounterTableRegistry? encounterTables = null, ClassRegistry? classRegistry = null, SynergyRegistry? synergies = null, FactionContentRepository? factionContent = null, RumorRepository? rumors = null)
    {
        Player = new Player(new Position(32, 32), Direction.North);
        LastUpdate = DateTime.UtcNow;
        _seed = seed ?? DateTime.UtcNow.GetHashCode();
        _encounterRng = new GameRandom(_seed);
        _encounterTables = encounterTables;
        _classRegistry = classRegistry;
        ExploredTiles = new BoundedTileSet(_exploredTilesSet, _exploredTilesOrder, MaxExploredTiles);
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

    private readonly HashSet<string> _exploredTilesSet = new();
    private readonly Queue<string> _exploredTilesOrder = new();
    private const int MaxExploredTiles = 4096;

    public BoundedTileSet ExploredTiles { get; private set; } = null!;

    public string? CurrentDungeonType { get; internal set; }

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
    }

    public bool ResolveTravelEncounter(string choice) => _overworldService.ResolveTravelEncounter(this, choice);

    public void Reset()
    {
        Mode = GameMode.Menu;
        CurrentDungeon = null;
        Combat = null;
        LastCombatResult = null;
        _stepsSinceEncounter = 0;
        Player = new Player(new Position(32, 32), Direction.North);
        ExploredTiles.Clear();
        Town = new TownState();
        Overworld = new OverworldState();
        WorldState.Reset();
        CampaignEnded = false;
        PartyGold = 500;
        TitheTokens = 0;
        PartyInventory.Clear();
        Party.DeadCharacters.Clear();
        InitializeDefaultParty();
        InitializeTown();
        ActionLog.Clear();
        _actionLogTurn = 0;
        _currentEncounterId = null;
        _pendingTaggedEncounterTile = null;
        Reputation.Clear();
        Evidence.Clear();
        AccusedFaction = null;
        MastermindAdvantage = false;
        FinalDungeonUnlocked = false;
        SettingsHash = null;
        CurrentTravelEncounter = null;
        RolledTravelEncounterCount = 0;
        ResolvedTravelEncounterCount = 0;
        LastUpdate = DateTime.UtcNow;
    }

    internal void ClearTaggedEncounterTile(bool resolved)
    {
        if (_pendingTaggedEncounterTile.HasValue && CurrentDungeon != null && resolved)
        {
            var pos = _pendingTaggedEncounterTile.Value;
            if (CurrentDungeon.IsValidPosition(pos))
            {
                var tile = CurrentDungeon.Tiles[pos.X, pos.Y];
                CurrentDungeon.Tiles[pos.X, pos.Y] = tile with { EncounterId = null };
            }
        }
        _pendingTaggedEncounterTile = null;
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

    public void ApplyReputationDelta(string factionId, int delta, string source) => _campaignService.ApplyReputationDelta(this, factionId, delta, source);

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

    public bool LoadGame(string? path = null) => Save.SaveSystem.Load(this, path, ContentHash);

    public bool ChooseBranch(Guid characterId, string branch) => _campaignService.ChooseBranch(this, characterId, branch);

    public bool RecruitFromTavern(string recruitId) => _townService.RecruitFromTavern(this, recruitId);

    public ResurrectionResult? ResurrectCharacter(Guid characterId) => _townService.ResurrectCharacter(this, characterId);

    public bool AcceptMission(string missionId) => _missionService.AcceptMission(this, missionId);

    public bool CompleteMission(string missionId) => _missionService.CompleteMission(this, missionId);

    public bool FailMission(string missionId) => _missionService.FailMission(this, missionId);

    public bool AbandonMission(string missionId) => _missionService.AbandonMission(this, missionId);

    public bool ApplyDialogueReputation(string factionId, int delta) => _campaignService.ApplyDialogueReputation(this, factionId, delta);

    public void SetReputation(string factionId, int value) => _campaignService.SetReputation(this, factionId, value);

    public bool PurchaseVendorItem(string itemId) => _townService.PurchaseVendorItem(this, itemId);

    public bool VerifyRumor(string rumorId, RumorVerificationSource source) => _townService.VerifyRumor(this, rumorId, source);
}

public enum GameMode
{
    Menu,
    Exploration,
    Combat,
    Dialog
}

public class BoundedTileSet
{
    private readonly HashSet<string> _set;
    private readonly Queue<string> _order;
    private readonly int _max;

    public BoundedTileSet(HashSet<string> set, Queue<string> order, int max)
    {
        _set = set;
        _order = order;
        _max = max;
    }

    public int Count => _set.Count;

    public bool Add(string key)
    {
        if (_set.Contains(key)) return false;
        if (_set.Count >= _max)
        {
            var oldest = _order.Dequeue();
            _set.Remove(oldest);
        }
        _set.Add(key);
        _order.Enqueue(key);
        return true;
    }

    public void Clear()
    {
        _set.Clear();
        _order.Clear();
    }

    public bool Contains(string key) => _set.Contains(key);

    public IEnumerable<string> AsEnumerable() => _set;

    public IEnumerator<string> GetEnumerator() => _set.GetEnumerator();
}
