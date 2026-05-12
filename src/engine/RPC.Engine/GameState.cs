using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Dungeons;
using RPC.Engine.Overworld;
using RPC.Engine.Party;
using RPC.Engine.Town;
using RPC.Engine.Travel;

namespace RPC.Engine;

public record ActionLogEntry(int Turn, string Category, string Type, Dictionary<string, string> Payload);
public record ParleyOffer(string EncounterId, string FactionId, string[] Options);

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
    public CombatState? Combat { get; private set; }
    public CombatResult? LastCombatResult { get; private set; }
    public List<CombatLogEntry> CombatLog => Combat?.Log ?? new List<CombatLogEntry>();
    public List<ActionLogEntry> ActionLog { get; } = new();
    public ReputationState Reputation { get; } = new();
    public EvidenceState Evidence { get; } = new();
    public JournalState Journal { get; } = new();
    public int PartyGold { get; set; } = 500;
    public List<string> PartyInventory { get; set; } = new();
    public bool CampaignEnded { get; set; } = false;
    public CampaignConfig? CampaignConfig { get; set; }
    public WorldState WorldState { get; set; } = new();
    public int CurrentAct => Overworld.Turns <= 15 ? 1 : Overworld.Turns <= 25 ? 2 : 3;
    public string? AccusedFaction { get; private set; }
    public bool MastermindAdvantage { get; private set; }
    public bool FinalDungeonUnlocked { get; private set; }
    public string? SettingsHash { get; set; }
    public TravelEncounterState? CurrentTravelEncounter { get; private set; }
    public int RolledTravelEncounterCount { get; private set; }
    public int ResolvedTravelEncounterCount { get; private set; }
    public ParleyOffer? CurrentParley { get; private set; }

    public void ClearCombatResult()
    {
        LastCombatResult = null;
    }

    private readonly GameRandom _encounterRng;
    private readonly EncounterTableRegistry? _encounterTables;
    private readonly ClassRegistry? _classRegistry;
    private readonly int _seed;
    private int _stepsSinceEncounter = 0;
    private int _actionLogTurn = 0;
    private string? _currentEncounterId;
    private Position? _pendingTaggedEncounterTile;

    public GameState(int? seed = null, EncounterTableRegistry? encounterTables = null, ClassRegistry? classRegistry = null)
    {
        Player = new Player(new Position(32, 32), Direction.North);
        LastUpdate = DateTime.UtcNow;
        _seed = seed ?? DateTime.UtcNow.GetHashCode();
        _encounterRng = new GameRandom(_seed);
        _encounterTables = encounterTables;
        _classRegistry = classRegistry;
        ExploredTiles = new BoundedTileSet(_exploredTilesSet, _exploredTilesOrder, MaxExploredTiles);
        InitializeDefaultParty();
        InitializeTown();
        Overworld = new OverworldState();
        Mode = GameMode.Menu; // Start in town/hub
    }

    private void InitializeDefaultParty()
    {
        Party.SetMember(0, new CharacterState(
            new Guid("11111111-1111-1111-1111-111111111111"), "Kael", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            new[] { "bone_spear", "tithe_touch" }, 0));
        Party.SetMember(1, new CharacterState(
            new Guid("22222222-2222-2222-2222-222222222222"), "Sera", "stillblade", 1, 0,
            new BaseStats(5, 5, 4, 3, 4), 14, Equipment.Empty,
            new[] { "rend", "silence_strike" }, 0));
        Party.SetMember(2, new CharacterState(
            new Guid("33333333-3333-3333-3333-333333333333"), "Mira", "cauterist", 1, 0,
            new BaseStats(3, 5, 4, 5, 4), 14, Equipment.Empty,
            new[] { "cauterize", "scalpel_dance" }, 0));
        Party.SetMember(3, new CharacterState(
            new Guid("44444444-4444-4444-4444-444444444444"), "Vex", "hollow", 1, 0,
            new BaseStats(4, 6, 3, 4, 4), 11, Equipment.Empty,
            new[] { "shiv", "smoke_bomb" }, 1));
        Party.SetMember(4, new CharacterState(
            new Guid("55555555-5555-5555-5555-555555555555"), "Nyx", "stillblade", 1, 0,
            new BaseStats(5, 5, 4, 3, 4), 14, Equipment.Empty,
            new[] { "rend", "silence_strike" }, 1));
        Party.SetMember(5, new CharacterState(
            new Guid("66666666-6666-6666-6666-666666666666"), "Orin", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            new[] { "bone_spear", "tithe_touch" }, 1));
    }

    private void InitializeTown()
    {
        if (Town.TavernRoster.Count == 0)
        {
            Town.TavernRoster = TavernRecruitGenerator.GenerateRoster(_seed);
        }
        if (Town.FactionContacts.Count == 0)
        {
            Town.FactionContacts = FactionContactGenerator.GenerateContacts();
        }
        if (Town.AvailableMissions.Count == 0)
        {
            Town.AvailableMissions = FactionContactGenerator.GenerateMissions();
        }
        if (Town.FactionVendors.Count == 0)
        {
            Town.FactionVendors = FactionVendorGenerator.GenerateStock();
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
        if (CampaignEnded) return;
        if (HasPendingBranchChoices) return;
        CurrentDungeon = dungeon;
        CurrentDungeonType = dungeonType;
        ExploredTiles.Clear();
        _stepsSinceEncounter = 0;
        _pendingTaggedEncounterTile = null;
        Mode = GameMode.Exploration;
        EmitActionLog("dungeon", "dungeon_entered", new Dictionary<string, string> { { "dungeonType", dungeonType } });
        IncrementTurns(1);
        // Find entrance position
        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                if (dungeon.Tiles[x, y].Type == TileType.Floor)
                {
                    Player.Position = new Position(x, y);
                    Player.Facing = Direction.North;
                    ExploreAroundPlayer();
                    return;
                }
            }
        }
    }

    public void ExploreAroundPlayer()
    {
        if (CurrentDungeon == null) return;
        var px = Player.Position.X;
        var py = Player.Position.Y;
        var viewRadius = 3;

        for (int x = Math.Max(0, px - viewRadius); x < Math.Min(CurrentDungeon.Width, px + viewRadius + 1); x++)
        {
            for (int y = Math.Max(0, py - viewRadius); y < Math.Min(CurrentDungeon.Height, py + viewRadius + 1); y++)
            {
                var tile = CurrentDungeon.Tiles[x, y];
                if (tile.Type != TileType.Empty)
                {
                    ExploredTiles.Add($"{x},{y}");
                }
            }
        }
    }

    public bool TryMoveForward() => ExecuteMove(Player.Facing);
    public bool TryMoveBack() => ExecuteMove(Player.Facing.Opposite());
    public bool TryStrafeLeft() => ExecuteMove(Player.Facing.StrafeLeft());
    public bool TryStrafeRight() => ExecuteMove(Player.Facing.StrafeRight());

    private bool ExecuteMove(Direction dir)
    {
        if (CurrentDungeon == null) return false;
        if (Mode == GameMode.Combat) return false;

        var newPos = Player.Position.Move(dir);
        if (CurrentDungeon.CanMoveTo(Player.Position, dir))
        {
            Player.Position = newPos;
            ExploreAroundPlayer();
            LastUpdate = DateTime.UtcNow;
            _stepsSinceEncounter++;

            var tile = CurrentDungeon.GetTile(newPos);
            if (!string.IsNullOrEmpty(tile.EncounterId))
            {
                var encounter = _encounterTables?.GetEncounterById(tile.EncounterId);
                if (encounter != null)
                {
                    _pendingTaggedEncounterTile = newPos;
                    TriggerEncounter(encounter);
                    return true;
                }
            }

            var encounterChance = 0.05 + (_stepsSinceEncounter * 0.08);
            if (_encounterRng.Roll(0, 99) < encounterChance * 100)
            {
                TriggerEncounter();
            }

            return true;
        }
        return false;
    }

    public void TurnLeft()
    {
        Player.TurnLeft();
        LastUpdate = DateTime.UtcNow;
    }

    public void TurnRight()
    {
        Player.TurnRight();
        LastUpdate = DateTime.UtcNow;
    }

    public void TriggerEncounter(EncounterDef? encounter = null)
    {
        _stepsSinceEncounter = 0;

        if (encounter == null)
        {
            var tableId = CurrentDungeon?.WanderingTableId ?? CurrentDungeon?.EncounterTableId;
            if (tableId != null && _encounterTables != null)
            {
                encounter = _encounterTables.RollEncounter(tableId, _encounterRng);
            }
        }

        if (encounter == null)
        {
            encounter = new EncounterDef("random", "Random Encounter", new[]
            {
                new EnemySpawn("rat", _encounterRng.Roll(1, 2)),
                new EnemySpawn("goblin_scavenger", _encounterRng.Roll(0, 1))
            });
        }

        // Check for faction soldier parley, Ashmouth negotiation, or hostility
        var factionId = _encounterTables?.GetEncounterFaction(encounter.Id);
        if (factionId != null)
        {
            var rep = Reputation[factionId];
            var hasAshmouth = Party.Members.Any(m => m.ClassId == "ashmouth" && m.IsAlive);
            var options = new List<string>();
            if (rep >= 25) options.Add("Parley");
            if (hasAshmouth) options.Add("Negotiate");
            options.Add("Fight");

            if (options.Count > 1) // More than just "Fight"
            {
                CurrentParley = new ParleyOffer(encounter.Id, factionId, options.ToArray());
                _currentEncounterId = Guid.NewGuid().ToString();
                Mode = GameMode.Exploration;
                EmitActionLog("combat", "encounter_parley_available", new Dictionary<string, string>
                {
                    { "encounterId", _currentEncounterId },
                    { "factionId", factionId }
                });
                LastUpdate = DateTime.UtcNow;
                return;
            }
            else if (rep < -25)
            {
                // Hostile: reinforce the encounter
                var reinforced = encounter.Enemies.Concat(new[] { new EnemySpawn("faction_soldier", 1) }).ToArray();
                encounter = encounter with { Enemies = reinforced };
            }
        }

        EnterCombat(encounter);
    }

    private void EnterCombat(EncounterDef encounter)
    {
        _currentEncounterId = Guid.NewGuid().ToString();
        Combat = CombatEngine.Enter(Party, encounter, new GameRandom(_encounterRng.Roll(1, 10000)));

        EmitActionLog("combat", "encounter_started", new Dictionary<string, string> { { "encounterId", _currentEncounterId } });

        if (Combat.IsFinished)
        {
            Mode = GameMode.Exploration;
            ClearTaggedEncounterTile(Combat.AllEnemiesDead);
            if (Combat.AllEnemiesDead && _currentEncounterId != null)
            {
                EmitActionLog("combat", "encounter_won", new Dictionary<string, string> { { "encounterId", _currentEncounterId } });
            }
            Combat = null;
        }
        else
        {
            Mode = GameMode.Combat;

            // Kick off the first round and auto-resolve any leading AI turns
            var rng = new GameRandom(_encounterRng.Roll(1, 10000));
            Combat = CombatEngine.Tick(Combat, null, rng, _classRegistry);
            while (!Combat.IsFinished && !(Combat.Phase == CombatPhase.Turn && Combat.CurrentActor?.IsPlayer == true))
            {
                Combat = CombatEngine.Tick(Combat, null, rng, _classRegistry);
            }
        }
        LastUpdate = DateTime.UtcNow;
    }

    public bool ResolveParley(string choice)
    {
        if (CurrentParley == null) return false;

        if (choice == "parley")
        {
            EmitActionLog("combat", "encounter_parleyed", new Dictionary<string, string>
            {
                { "encounterId", _currentEncounterId ?? "unknown" },
                { "factionId", CurrentParley.FactionId }
            });
            CurrentParley = null;
            Mode = GameMode.Exploration;
            return true;
        }
        else if (choice == "negotiate")
        {
            return ResolveAshmouthNegotiation();
        }
        else
        {
            var encounter = _encounterTables?.GetEncounterById(CurrentParley.EncounterId);
            if (encounter == null)
            {
                encounter = new EncounterDef("random", "Random Encounter", new[]
                {
                    new EnemySpawn("rat", _encounterRng.Roll(1, 2)),
                    new EnemySpawn("goblin_scavenger", _encounterRng.Roll(0, 1))
                });
            }
            CurrentParley = null;
            EnterCombat(encounter);
            return true;
        }
    }

    private bool ResolveAshmouthNegotiation()
    {
        if (CurrentParley == null) return false;

        var factionId = CurrentParley.FactionId;
        var encounter = _encounterTables?.GetEncounterById(CurrentParley.EncounterId);

        var ashmouth = Party.Members
            .Where(m => m.ClassId == "ashmouth" && m.IsAlive)
            .OrderByDescending(m => m.Level)
            .FirstOrDefault();

        if (ashmouth.Id == Guid.Empty)
        {
            CurrentParley = null;
            return false;
        }

        // Enemy leader level approximated by encounter danger
        var leaderLevel = 2;
        var repModifier = Reputation[factionId] / 10;
        var successThreshold = leaderLevel - repModifier;
        var roll = _encounterRng.Roll(1, 6);
        var total = ashmouth.Level + roll;

        if (total >= successThreshold + 3)
        {
            // Complete success
            EmitActionLog("combat", "negotiation_complete_success", new Dictionary<string, string>
            {
                { "encounterId", _currentEncounterId ?? "unknown" },
                { "factionId", factionId },
                { "ashmouthLevel", ashmouth.Level.ToString() },
                { "roll", roll.ToString() }
            });
            CurrentParley = null;
            Mode = GameMode.Exploration;
            return true;
        }
        else if (total >= successThreshold)
        {
            // Partial success
            EmitActionLog("combat", "negotiation_partial_success", new Dictionary<string, string>
            {
                { "encounterId", _currentEncounterId ?? "unknown" },
                { "factionId", factionId },
                { "ashmouthLevel", ashmouth.Level.ToString() },
                { "roll", roll.ToString() }
            });
            CurrentParley = null;
            Mode = GameMode.Exploration;
            return true;
        }
        else
        {
            // Failure - combat with surprise round
            EmitActionLog("combat", "negotiation_failure", new Dictionary<string, string>
            {
                { "encounterId", _currentEncounterId ?? "unknown" },
                { "factionId", factionId },
                { "ashmouthLevel", ashmouth.Level.ToString() },
                { "roll", roll.ToString() }
            });

            if (encounter == null)
            {
                encounter = new EncounterDef("random", "Random Encounter", new[]
                {
                    new EnemySpawn("rat", _encounterRng.Roll(1, 2)),
                    new EnemySpawn("goblin_scavenger", _encounterRng.Roll(0, 1))
                });
            }
            CurrentParley = null;
            EnterCombatWithSurprise(encounter);
            return true;
        }
    }

    private void EnterCombatWithSurprise(EncounterDef encounter)
    {
        EnterCombat(encounter);

        if (Combat != null && !Combat.IsFinished)
        {
            var enemies = Combat.Combatants.Where(c => !c.IsPlayer && c.IsAlive).ToArray();
            var players = Combat.Combatants.Where(c => c.IsPlayer && c.IsAlive).ToArray();

            var newCombatants = Combat.Combatants.ToArray();
            var newLog = new List<CombatLogEntry>(Combat.Log);

            foreach (var enemy in enemies)
            {
                if (players.Length == 0) break;
                var target = players[_encounterRng.Roll(0, players.Length - 1)];
                var targetIdx = Array.FindIndex(newCombatants, c => c.Id == target.Id);
                if (targetIdx < 0) continue;

                var damage = _encounterRng.Roll(1, 4) + 1;
                var newHp = Math.Max(0, target.Hp - damage);
                newCombatants[targetIdx] = newCombatants[targetIdx] with { Hp = newHp };
                newLog.Add(new(enemy.Id, $"{enemy.Name} surprises {target.Name} for {damage} damage!", Combat.Round));
            }

            Combat = Combat with { Combatants = newCombatants, Log = newLog };
        }
    }

    public bool SubmitCombatAction(CombatAction action)
    {
        if (Combat == null || Mode != GameMode.Combat) return false;

        // Validate ability row requirements
        if (action.Type == ActionType.UseAbility && action.AbilityId is not null)
        {
            var actor = Combat.Combatants.FirstOrDefault(c => c.Id == action.ActorId);
            if (actor.Id != Guid.Empty)
            {
                var member = Party.Members.FirstOrDefault(m => m.Id == action.ActorId);
                if (member.Id != Guid.Empty && _classRegistry?.Get(member.ClassId) is { } classDef)
                {
                    var ability = classDef.Abilities.FirstOrDefault(a => a.Id == action.AbilityId);
                    if (ability is not null && !ability.IsAvailableInRow(actor.Row))
                        return false;
                }
            }
        }

        var rng = new GameRandom(_encounterRng.Roll(1, 10000));
        Action<string, string, Dictionary<string, string>> emitter = (cat, type, payload) =>
        {
            if (type == "synergy_triggered" && _currentEncounterId != null)
            {
                payload["encounterId"] = _currentEncounterId;
            }
            if (type == "synergy_triggered" && payload.TryGetValue("synergyId", out var sid) && !string.IsNullOrEmpty(sid))
            {
                Journal.Discover(sid);
            }
            EmitActionLog(cat, type, payload);
        };

        Combat = CombatEngine.Tick(Combat, action, rng, _classRegistry, emitter);

        // Auto-resolve AI turns
        while (!Combat.IsFinished && !(Combat.Phase == CombatPhase.Turn && Combat.CurrentActor?.IsPlayer == true))
        {
            Combat = CombatEngine.Tick(Combat, null, rng, _classRegistry, emitter);
        }

        if (Combat.IsFinished)
        {
            var allEnemiesDead = Combat.AllEnemiesDead;

            // Apply combat results to party
            var levelUps = new List<string>();
            foreach (var combatant in Combat.Combatants.Where(c => c.IsPlayer))
            {
                var member = Party.Members.FirstOrDefault(m => m.Id == combatant.Id);
                if (member.Id != Guid.Empty)
                {
                    var index = Array.IndexOf(Party.Members, member);
                    var newXp = member.Xp + Combat.XpReward;
                    var updated = member with { CurrentHp = combatant.Hp, Xp = newXp, TempModifiers = combatant.TempModifiers };

                    // Check for level ups
                    if (_classRegistry?.Get(member.ClassId) is { } classDef)
                    {
                        var beforeLevel = updated.Level;
                        updated = LevelingSystem.CheckAndApplyLevelUps(updated, classDef);
                        if (updated.Level > beforeLevel)
                        {
                            levelUps.Add(updated.Name);
                        }
                    }

                    Party.SetMember(index, updated);
                }
            }

            LastCombatResult = new CombatResult(
                allEnemiesDead,
                Combat.XpReward,
                levelUps.ToArray(),
                Combat.Round);

            Mode = GameMode.Exploration;
            Combat = null;

            ClearTaggedEncounterTile(allEnemiesDead);

            if (allEnemiesDead && _currentEncounterId != null)
            {
                EmitActionLog("combat", "encounter_won", new Dictionary<string, string> { { "encounterId", _currentEncounterId } });
            }
        }

        LastUpdate = DateTime.UtcNow;
        return true;
    }

    public void FleeCombat()
    {
        if (Mode != GameMode.Combat) return;
        Mode = GameMode.Exploration;
        Combat = null;
        ClearTaggedEncounterTile(resolved: false);
        LastUpdate = DateTime.UtcNow;
    }

    public void RestAtInn()
    {
        if (Mode != GameMode.Menu) return;
        foreach (var member in Party.Members)
        {
            if (member.Id == Guid.Empty) continue;
            var maxHp = member.GetEffectiveStats().MaxHp;
            var index = Array.IndexOf(Party.Members, member);
            Party.SetMember(index, member with { CurrentHp = maxHp, TempModifiers = Array.Empty<TempStatModifier>() });
        }
        LastUpdate = DateTime.UtcNow;
    }

    public void ReturnToTown()
    {
        if (CurrentDungeon != null)
        {
            EmitActionLog("dungeon", "dungeon_completed", new Dictionary<string, string> { { "dungeonType", CurrentDungeonType ?? "" } });
        }
        Mode = GameMode.Menu;
        CurrentDungeon = null;
        LastUpdate = DateTime.UtcNow;
        IncrementTurns(1);
    }

    public void GenerateOverworld(CampaignConfig config)
    {
        Overworld.GenerateFromConfig(config, _encounterRng);
        SyncWorldStateFromOverworld();
    }

    public FactionState GetFactionState(string factionId)
    {
        if (CampaignConfig?.FactionTimelines.TryGetValue(factionId, out var timeline) == true)
        {
            if (Overworld.Turns >= timeline.Executing)
                return FactionState.Executing;
            if (Overworld.Turns >= timeline.Preparing)
                return FactionState.Preparing;
        }
        return FactionState.Investigating;
    }

    private void SyncWorldStateFromOverworld()
    {
        WorldState.Settlements = Overworld.Nodes.Values
            .Where(n => n.Type == NodeType.Town)
            .ToDictionary(n => n.Id, _ => "pending");

        WorldState.AccessibleDungeons = Overworld.Nodes.Values
            .Where(n => n.Type == NodeType.Dungeon)
            .Select(n => n.Id)
            .ToList();

        WorldState.FactionTerritory.Clear();
        foreach (var node in Overworld.Nodes.Values)
        {
            foreach (var faction in node.FactionPresence)
            {
                if (!WorldState.FactionTerritory.ContainsKey(faction))
                    WorldState.FactionTerritory[faction] = new List<string>();
                if (!WorldState.FactionTerritory[faction].Contains(node.Id))
                    WorldState.FactionTerritory[faction].Add(node.Id);
            }
        }
    }

    public bool Travel(string targetId)
    {
        if (CampaignEnded) return false;
        if (HasPendingBranchChoices) return false;
        var fromNodeId = Overworld.CurrentNodeId;
        var route = Overworld.GetRoute(fromNodeId, targetId);
        var changed = Overworld.Travel(targetId);
        if (!changed) return false;

        EmitActionLog("overworld", "travel_started", new Dictionary<string, string>
        {
            { "from", fromNodeId },
            { "to", targetId },
            { "distance", route?.Distance.ToString() ?? "0" }
        });

        if (route != null)
        {
            IncrementTurns(route.Distance);
        }

        ClearTravelEncounters();

        if (route != null)
        {
            RollTravelEncounters(route.DangerRating);
        }

        if (RolledTravelEncounterCount == 0)
        {
            if (Overworld.Nodes.TryGetValue(targetId, out var node) && node.Type == NodeType.Town)
            {
                EmitActionLog("overworld", "town_reached", new Dictionary<string, string>
                {
                    { "townId", targetId }
                });
            }
        }

        LastUpdate = DateTime.UtcNow;
        return true;
    }

    private void IncrementTurns(int amount)
    {
        if (CampaignEnded || amount <= 0) return;
        var oldTurn = Overworld.Turns;
        Overworld.Turns = Math.Min(35, Overworld.Turns + amount);
        var newTurn = Overworld.Turns;

        if (oldTurn < 12 && newTurn >= 12)
        {
            EmitActionLog("campaign", "faction_progression", new Dictionary<string, string> { { "milestone", "12" } });
        }
        if (oldTurn < 22 && newTurn >= 22)
        {
            EmitActionLog("campaign", "faction_progression", new Dictionary<string, string> { { "milestone", "22" } });
        }

        if (Overworld.Turns >= 35)
        {
            CampaignEnded = true;
            CurrentDungeon = null;
            Mode = GameMode.Menu;
        }
    }

    private void ClearTravelEncounters()
    {
        CurrentTravelEncounter = null;
        RolledTravelEncounterCount = 0;
        ResolvedTravelEncounterCount = 0;
    }

    private void RollTravelEncounters(int dangerRating)
    {
        var count = TravelEncounterTable.RollEncounterCount(_encounterRng);
        RolledTravelEncounterCount = count;
        if (count == 0) return;

        var encounter = TravelEncounterTable.RollEncounter(_encounterRng, dangerRating);
        if (encounter == null) return;

        ActivateTravelEncounter(encounter);
    }

    private static bool IsPatrolEncounter(string id) => id == "faction_patrol" || id.EndsWith("_patrol");

    private void ActivateTravelEncounter(TravelEncounterDef encounter)
    {
        bool hasMarcher = Party.Members.Any(m => m.ClassId == "marcher");
        bool hasAshmouth = Party.Members.Any(m => m.ClassId == "ashmouth");

        bool surpriseRound = encounter.ResolutionType == TravelResolutionType.Combat
            && encounter.Id == "ambush"
            && !hasMarcher;

        int priceTier = encounter.Id == "merchant" && hasAshmouth ? 1 : 0;

        int repValue = 0;
        string? factionId = encounter.FactionId;
        if (factionId != null)
        {
            repValue = Reputation[factionId];
        }

        bool isHostilePatrol = IsPatrolEncounter(encounter.Id) && factionId != null && repValue < 0;

        string[]? options = null;
        string resolutionType;

        if (isHostilePatrol)
        {
            resolutionType = "combat";
        }
        else if (encounter.ResolutionType == TravelResolutionType.Dialogue)
        {
            resolutionType = "dialogue";
            if (IsPatrolEncounter(encounter.Id))
            {
                options = repValue >= 25
                    ? ["Request intel", "Trade supplies", "Pass safely"]
                    : ["Show papers", "Bribe", "Attack"];
            }
            else
            {
                options = encounter.Id == "merchant"
                    ? ["Trade", "Ignore", "Rob"]
                    : ["Trade", "Ignore", "Help"];
            }
        }
        else
        {
            resolutionType = encounter.ResolutionType switch
            {
                TravelResolutionType.Combat => "combat",
                TravelResolutionType.StatTest => "stat_test",
                _ => "unknown"
            };
        }

        CurrentTravelEncounter = new TravelEncounterState(
            encounter.Id,
            encounter.Name,
            resolutionType,
            encounter.StatName,
            factionId,
            repValue,
            surpriseRound,
            priceTier,
            options);

        if (isHostilePatrol)
        {
            var patrolEnemies = new[] { new EnemySpawn("faction_soldier", 2) };
            var encounterDef = new EncounterDef(encounter.Id, encounter.Name, patrolEnemies, 15);
            Combat = CombatEngine.Enter(Party, encounterDef, new GameRandom(_encounterRng.Roll(1, 10000)));
            CurrentTravelEncounter = null;

            if (Combat.IsFinished)
            {
                Mode = GameMode.Menu;
                Combat = null;
                ResolveTravelEncounter("auto");
            }
            else
            {
                Mode = GameMode.Combat;
                var rng = new GameRandom(_encounterRng.Roll(1, 10000));
                Combat = CombatEngine.Tick(Combat, null, rng, _classRegistry);
                while (!Combat.IsFinished && !(Combat.Phase == CombatPhase.Turn && Combat.CurrentActor?.IsPlayer == true))
                {
                    Combat = CombatEngine.Tick(Combat, null, rng, _classRegistry);
                }
            }
        }
        else if (encounter.ResolutionType == TravelResolutionType.Combat && encounter.Enemies != null)
        {
            var encounterDef = new EncounterDef(encounter.Id, encounter.Name, encounter.Enemies, 15);
            Combat = CombatEngine.Enter(Party, encounterDef, new GameRandom(_encounterRng.Roll(1, 10000)));
            CurrentTravelEncounter = null;

            if (Combat.IsFinished)
            {
                Mode = GameMode.Menu;
                Combat = null;
                ResolveTravelEncounter("auto");
            }
            else
            {
                Mode = GameMode.Combat;
                var rng = new GameRandom(_encounterRng.Roll(1, 10000));
                Combat = CombatEngine.Tick(Combat, null, rng, _classRegistry);
                while (!Combat.IsFinished && !(Combat.Phase == CombatPhase.Turn && Combat.CurrentActor?.IsPlayer == true))
                {
                    Combat = CombatEngine.Tick(Combat, null, rng, _classRegistry);
                }
            }
        }
    }

    public bool ResolveTravelEncounter(string choice)
    {
        if (CurrentTravelEncounter == null) return false;

        var encounter = CurrentTravelEncounter;
        if (encounter.ResolutionType == "stat_test" && encounter.StatName != null)
        {
            var highestStat = Party.Members
                .Where(m => m.IsAlive)
                .Max(m => encounter.StatName switch
                {
                    "strength" => m.BaseStats.Strength,
                    "dexterity" => m.BaseStats.Dexterity,
                    "constitution" => m.BaseStats.Constitution,
                    "intelligence" => m.BaseStats.Intelligence,
                    "willpower" => m.BaseStats.Willpower,
                    _ => 0
                });

            var roll = _encounterRng.Roll(1, 20);
            var success = roll + highestStat >= 15;

            EmitActionLog("travel", "stat_test", new Dictionary<string, string>
            {
                { "encounterId", encounter.Id },
                { "stat", encounter.StatName },
                { "highest", highestStat.ToString() },
                { "roll", roll.ToString() },
                { "success", success.ToString() }
            });
        }
        else if (encounter.ResolutionType == "dialogue")
        {
            EmitActionLog("travel", "dialogue", new Dictionary<string, string>
            {
                { "encounterId", encounter.Id },
                { "choice", choice },
                { "factionId", encounter.FactionId ?? "none" }
            });

            // Apply reputation effects from travel dialogue choices
            if (encounter.FactionId != null && IsPatrolEncounter(encounter.Id))
            {
                switch (choice)
                {
                    case "Attack":
                        Reputation.ApplyDelta(encounter.FactionId, -5, "travel_encounter:attack_patrol");
                        break;
                    case "Request intel":
                    case "Pass safely":
                        Reputation.ApplyDelta(encounter.FactionId, 2, "travel_encounter:cooperate");
                        break;
                }
            }
            else if (encounter.Id == "refugees" && choice == "Help")
            {
                Reputation.ApplyDelta("bureau", 2, "travel_encounter:help_refugees");
            }
        }

        EmitActionLog("overworld", "travel_encounter_resolved", new Dictionary<string, string>
        {
            { "encounterId", encounter.Id },
            { "resolutionType", encounter.ResolutionType },
            { "choice", choice }
        });

        ResolvedTravelEncounterCount++;
        CurrentTravelEncounter = null;

        if (ResolvedTravelEncounterCount < RolledTravelEncounterCount)
        {
            var route = Overworld.Routes.FirstOrDefault(r =>
                r.From == Overworld.CurrentNodeId || r.To == Overworld.CurrentNodeId);
            var next = TravelEncounterTable.RollEncounter(_encounterRng, route?.DangerRating ?? 0);
            if (next != null)
            {
                ActivateTravelEncounter(next);
            }
        }

        if (ResolvedTravelEncounterCount >= RolledTravelEncounterCount)
        {
            var node = Overworld.Nodes.GetValueOrDefault(Overworld.CurrentNodeId);
            if (node?.Type == NodeType.Town)
            {
                EmitActionLog("overworld", "town_reached", new Dictionary<string, string>
                {
                    { "townId", Overworld.CurrentNodeId }
                });
            }
        }

        LastUpdate = DateTime.UtcNow;
        return true;
    }

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
        PartyInventory.Clear();
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
        ClearTravelEncounters();
        LastUpdate = DateTime.UtcNow;
    }

    private void ClearTaggedEncounterTile(bool resolved)
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

    private void EmitActionLog(string category, string type, Dictionary<string, string> payload)
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

    public void DiscoverSecret(string secretType, string secretId)
    {
        EmitActionLog("dungeon", "secret_discovered", new Dictionary<string, string>
        {
            { "secretType", secretType },
            { "secretId", secretId }
        });
        LastUpdate = DateTime.UtcNow;
    }

    public void ChooseSettlementFate(string settlementId, string fate)
    {
        WorldState.Settlements[settlementId] = fate;
        EmitActionLog("dungeon", "settlement_fate_chosen", new Dictionary<string, string>
        {
            { "settlementId", settlementId },
            { "fate", fate }
        });
        LastUpdate = DateTime.UtcNow;
    }

    public void SaveGame(string? path = null) => Save.SaveSystem.Save(this, path);
    public void ApplyReputationDelta(string factionId, int delta, string source)
    {
        var changes = Reputation.ApplyDelta(factionId, delta, source);
        foreach (var change in changes)
        {
            EmitActionLog("faction", "rep_changed", new Dictionary<string, string>
            {
                { "factionId", change.FactionId },
                { "delta", change.Delta.ToString() },
                { "newValue", change.NewValue.ToString() },
                { "source", change.Source }
            });
        }
        LastUpdate = DateTime.UtcNow;
    }

    public void AddEvidence(string factionId, string source, int amount = 1)
    {
        var result = Evidence.AddEvidence(factionId, source, amount);
        EmitActionLog("evidence", "evidence_added", new Dictionary<string, string>
        {
            { "factionId", result.FactionId },
            { "amount", result.Amount.ToString() },
            { "newValue", result.NewValue.ToString() },
            { "source", result.Source },
            { "threshold", result.ThresholdReached.ToString() }
        });
        LastUpdate = DateTime.UtcNow;
    }

    public int GetEvidenceThreshold(string factionId) => Evidence.GetThreshold(factionId);

    public bool AccuseFaction(string factionId)
    {
        if (CampaignConfig == null) return false;
        if (Evidence.GetThreshold(factionId) < 7) return false;
        if (AccusedFaction != null) return false;

        AccusedFaction = factionId;
        var isCorrect = factionId == CampaignConfig.Mastermind;

        if (isCorrect)
        {
            EmitActionLog("mastermind", "accusation_correct", new Dictionary<string, string>
            {
                { "factionId", factionId }
            });
        }
        else
        {
            MastermindAdvantage = true;
            ApplyReputationDelta(factionId, -20, "wrong_accusation");
            EmitActionLog("mastermind", "accusation_wrong", new Dictionary<string, string>
            {
                { "factionId", factionId },
                { "penalty", "-20" }
            });
        }

        LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool UnlockFinalDungeon()
    {
        if (CampaignConfig == null) return false;
        if (AccusedFaction != CampaignConfig.Mastermind) return false;
        if (!Evidence.Counters.Values.Any(v => v >= 10)) return false;
        if (FinalDungeonUnlocked) return false;

        FinalDungeonUnlocked = true;
        EmitActionLog("mastermind", "final_dungeon_unlocked", new Dictionary<string, string>
        {
            { "mastermind", CampaignConfig.Mastermind }
        });
        LastUpdate = DateTime.UtcNow;
        return true;
    }

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

    public bool LoadGame(string? path = null) => Save.SaveSystem.Load(this, path);

    public bool ChooseBranch(Guid characterId, string branch)
    {
        var member = Party.Members.FirstOrDefault(m => m.Id == characterId);
        if (member.Id == Guid.Empty || member.Level < 3) return false;
        if (_classRegistry?.Get(member.ClassId) is not { } classDef) return false;

        if (member.BranchChoice == null && TryResolveLevel3Branch(member, branch, classDef, out var resolved3))
        {
            ApplyBranchToMember(member, resolved3, "3", classDef);
            return true;
        }

        if (member.Level >= 6 && member.BranchLevel6 == null && TryResolveLevel6Branch(member, branch, classDef, out var resolved6))
        {
            ApplyBranchToMember(member, resolved6, "6", classDef);
            return true;
        }

        return false;
    }

    private bool TryResolveLevel3Branch(CharacterState member, string branch, ClassDef classDef, out string resolvedBranch)
    {
        resolvedBranch = branch;
        var available = classDef.AvailableBranches ?? classDef.Branches?.Where(b => b.RequiresBranch == null).Select(b => b.Id).ToArray() ?? Array.Empty<string>();
        return available.Contains(branch);
    }

    private bool TryResolveLevel6Branch(CharacterState member, string branch, ClassDef classDef, out string resolvedBranch)
    {
        resolvedBranch = branch;
        var available = classDef.Branches?.Where(b => b.RequiresBranch == member.BranchChoice).Select(b => b.Id).ToArray() ?? Array.Empty<string>();
        if (!available.Contains(branch)) return false;

        var branchDef = classDef.Branches?.FirstOrDefault(b => b.Id == branch);
        if (branchDef?.FactionGate is { } gate && Reputation[gate.FactionId] < gate.Threshold)
        {
            var fallback = branchDef.FallbackBranch;
            if (string.IsNullOrEmpty(fallback)) return false;
            resolvedBranch = fallback;

            EmitActionLog("branch", "branch_fallback", new Dictionary<string, string>
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

    private void ApplyBranchToMember(CharacterState member, string resolvedBranch, string levelLabel, ClassDef classDef)
    {
        var branchAbilities = classDef.Abilities
            .Where(a => a.Branch == resolvedBranch)
            .Select(a => a.Id)
            .ToArray();

        var newAbilities = member.KnownAbilities
            .Concat(branchAbilities)
            .Distinct()
            .ToArray();

        var index = Array.IndexOf(Party.Members, member);
        Party.SetMember(index, levelLabel == "3"
            ? member with { BranchChoice = resolvedBranch, KnownAbilities = newAbilities }
            : member with { BranchLevel6 = resolvedBranch, KnownAbilities = newAbilities });

        EmitActionLog("branch", "branch_chosen", new Dictionary<string, string>
        {
            { "characterId", member.Id.ToString() },
            { "branch", resolvedBranch },
            { "level", levelLabel }
        });

        LastUpdate = DateTime.UtcNow;
    }

    public bool RecruitFromTavern(string recruitId)
    {
        var recruit = Town.TavernRoster.FirstOrDefault(r => r.Id == recruitId);
        if (recruit == null) return false;

        var emptySlot = Array.IndexOf(Party.Members, default);
        if (emptySlot < 0) return false;

        var maxHp = EffectiveStats.FromBase(recruit.BaseStats, recruit.Level).MaxHp;
        var character = new CharacterState(
            Guid.NewGuid(), recruit.Name, recruit.ClassId,
            recruit.Level, 0, recruit.BaseStats, maxHp,
            Equipment.Empty, Array.Empty<string>(), 0);

        Party.SetMember(emptySlot, character);
        Town.TavernRoster.Remove(recruit);
        LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool AcceptMission(string missionId)
    {
        var mission = Town.AvailableMissions.FirstOrDefault(m => m.Id == missionId);
        if (mission == null) return false;

        Town.AvailableMissions.Remove(mission);
        Town.QuestLog.Add(new ActiveMission(mission.Id, mission.Title, mission.Description, mission.RepReward, mission.FactionId, "active", mission.Type));
        LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool CompleteMission(string missionId)
    {
        var mission = Town.QuestLog.FirstOrDefault(m => m.Id == missionId && m.Status == "active");
        if (mission == null) return false;

        var index = Town.QuestLog.FindIndex(m => m.Id == missionId);
        Town.QuestLog[index] = mission with { Status = "completed" };

        var (primaryDelta, opposedDelta) = mission.Type switch
        {
            MissionType.Main => (8, -4),
            _ => (5, -2),
        };

        var vendorThreshold = Town.FactionVendors.FirstOrDefault(v => v.FactionId == mission.FactionId)?.Threshold ?? 25;
        var oldPrimaryRep = Reputation[mission.FactionId];
        var wasUnlocked = oldPrimaryRep >= vendorThreshold;

        var source = $"mission_complete_{mission.Type.ToString().ToLower()}";
        ApplyMissionReputation(mission.FactionId, primaryDelta, opposedDelta, source);

        EmitActionLog("faction", "mission_completed", new Dictionary<string, string>
        {
            { "missionId", mission.Id },
            { "factionId", mission.FactionId },
            { "type", mission.Type.ToString().ToLower() }
        });

        var newPrimaryRep = Reputation[mission.FactionId];
        if (!wasUnlocked && newPrimaryRep >= vendorThreshold)
        {
            EmitActionLog("faction", "vendor_unlocked", new Dictionary<string, string>
            {
                { "factionId", mission.FactionId },
                { "threshold", vendorThreshold.ToString() }
            });
        }

        LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool FailMission(string missionId)
    {
        var mission = Town.QuestLog.FirstOrDefault(m => m.Id == missionId && m.Status == "active");
        if (mission == null) return false;

        var index = Town.QuestLog.FindIndex(m => m.Id == missionId);
        Town.QuestLog[index] = mission with { Status = "failed" };

        ApplyMissionReputation(mission.FactionId, -3, 1, "mission_failed");

        EmitActionLog("faction", "mission_failed", new Dictionary<string, string>
        {
            { "missionId", mission.Id },
            { "factionId", mission.FactionId }
        });

        LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool AbandonMission(string missionId)
    {
        var mission = Town.QuestLog.FirstOrDefault(m => m.Id == missionId && m.Status == "active");
        if (mission == null) return false;

        var index = Town.QuestLog.FindIndex(m => m.Id == missionId);
        Town.QuestLog[index] = mission with { Status = "abandoned" };

        ApplyMissionReputation(mission.FactionId, -3, 1, "mission_abandoned");
        LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool ApplyDialogueReputation(string factionId, int delta)
    {
        ApplyReputationDelta(factionId, delta, "dialogue_choice");
        return true;
    }

    private void ApplyMissionReputation(string factionId, int primaryDelta, int opposedDelta, string source)
    {
        var primaryChanges = Reputation.ApplyDelta(factionId, primaryDelta, source, propagate: false);
        foreach (var change in primaryChanges)
        {
            EmitActionLog("faction", "rep_changed", new Dictionary<string, string>
            {
                { "factionId", change.FactionId },
                { "delta", change.Delta.ToString() },
                { "newValue", change.NewValue.ToString() },
                { "source", change.Source }
            });
        }

        if (opposedDelta != 0)
        {
            var opposedFactionId = Reputation.GetOpposedFaction(factionId);
            if (opposedFactionId != null)
            {
                var opposedChanges = Reputation.ApplyDelta(opposedFactionId, opposedDelta, source, propagate: false);
                foreach (var change in opposedChanges)
                {
                    EmitActionLog("faction", "rep_changed", new Dictionary<string, string>
                    {
                        { "factionId", change.FactionId },
                        { "delta", change.Delta.ToString() },
                        { "newValue", change.NewValue.ToString() },
                        { "source", change.Source }
                    });
                }
            }
        }
    }

    public void SetReputation(string factionId, int value)
    {
        Reputation[factionId] = value;
        LastUpdate = DateTime.UtcNow;
    }

    public bool PurchaseVendorItem(string itemId)
    {
        var genericItem = Town.VendorStock.FirstOrDefault(v => v.ItemId == itemId);
        if (genericItem != null)
            return CompletePurchase(itemId, genericItem.Price, Town.VendorStock, null);

        foreach (var vendor in Town.FactionVendors)
        {
            var item = vendor.Stock.FirstOrDefault(v => v.ItemId == itemId);
            if (item != null)
            {
                if (Reputation[vendor.FactionId] < vendor.Threshold) return false;
                return CompletePurchase(itemId, item.Price, vendor.Stock, vendor.FactionId);
            }
        }

        return false;
    }

    private bool CompletePurchase(string itemId, int price, List<VendorItem> stock, string? factionId)
    {
        if (PartyGold < price) return false;
        PartyGold -= price;
        PartyInventory.Add(itemId);
        var item = stock.First(v => v.ItemId == itemId);
        stock.Remove(item);
        var payload = new Dictionary<string, string> { { "itemId", itemId }, { "price", price.ToString() } };
        if (factionId != null) payload["factionId"] = factionId;
        EmitActionLog("town", "vendor_purchase", payload);
        LastUpdate = DateTime.UtcNow;
        return true;
    }
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

    public void Add(string key)
    {
        if (_set.Contains(key)) return;
        if (_set.Count >= _max)
        {
            var oldest = _order.Dequeue();
            _set.Remove(oldest);
        }
        _set.Add(key);
        _order.Enqueue(key);
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
