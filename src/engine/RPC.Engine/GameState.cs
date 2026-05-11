using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Dungeons;
using RPC.Engine.Party;
using RPC.Engine.Town;

namespace RPC.Engine;

public record ActionLogEntry(int Turn, string Category, string Type, Dictionary<string, string> Payload);

public class GameState
{
    public Player Player { get; set; }
    public Dungeon? CurrentDungeon { get; set; }
    public GameMode Mode { get; set; } = GameMode.Exploration;
    public DateTime LastUpdate { get; set; }
    public PartyState Party { get; set; } = new();
    public TownState Town { get; set; } = new();
    public CombatState? Combat { get; private set; }
    public CombatResult? LastCombatResult { get; private set; }
    public List<CombatLogEntry> CombatLog => Combat?.Log ?? new List<CombatLogEntry>();
    public List<ActionLogEntry> ActionLog { get; } = new();
    public Dictionary<string, int> Reputation { get; } = new();
    public string? SettingsHash { get; set; }

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
            new[] { "cauterize", "scalpel_dance" }, 1));
        Party.SetMember(3, new CharacterState(
            new Guid("44444444-4444-4444-4444-444444444444"), "Vex", "hollow", 1, 0,
            new BaseStats(4, 6, 3, 4, 4), 11, Equipment.Empty,
            new[] { "shiv", "smoke_bomb" }, 1));
    }

    private void InitializeTown()
    {
        if (Town.TavernRoster.Count == 0)
        {
            Town.TavernRoster = TavernRecruitGenerator.GenerateRoster(_seed);
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

    public void EnterDungeon(Dungeon dungeon, string dungeonType)
    {
        CurrentDungeon = dungeon;
        CurrentDungeonType = dungeonType;
        ExploredTiles.Clear();
        _stepsSinceEncounter = 0;
        Mode = GameMode.Exploration;
        EmitActionLog("dungeon", "dungeon_entered", new Dictionary<string, string> { { "dungeonType", dungeonType } });
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

    public bool TryMoveForward()
    {
        if (CurrentDungeon == null) return false;
        if (Mode == GameMode.Combat) return false;

        var newPos = Player.Position.Move(Player.Facing);
        if (CurrentDungeon.CanMoveTo(Player.Position, Player.Facing))
        {
            Player.Position = newPos;
            ExploreAroundPlayer();
            LastUpdate = DateTime.UtcNow;
            _stepsSinceEncounter++;

            // Random encounter check: increases with each step
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

        if (encounter == null && CurrentDungeon?.EncounterTableId != null && _encounterTables != null)
        {
            encounter = _encounterTables.RollEncounter(CurrentDungeon.EncounterTableId, _encounterRng);
        }

        if (encounter == null)
        {
            encounter = new EncounterDef("random", "Random Encounter", new[]
            {
                new EnemySpawn("rat", _encounterRng.Roll(1, 2)),
                new EnemySpawn("goblin_scavenger", _encounterRng.Roll(0, 1))
            });
        }

        _currentEncounterId = Guid.NewGuid().ToString();
        Combat = CombatEngine.Enter(Party, encounter, new GameRandom(_encounterRng.Roll(1, 10000)));

        EmitActionLog("combat", "encounter_started", new Dictionary<string, string> { { "encounterId", _currentEncounterId } });

        if (Combat.IsFinished)
        {
            Mode = GameMode.Exploration;
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
            Combat = CombatEngine.Tick(Combat, null, rng);
            while (!Combat.IsFinished && !(Combat.Phase == CombatPhase.Turn && Combat.CurrentActor?.IsPlayer == true))
            {
                Combat = CombatEngine.Tick(Combat, null, rng);
            }
        }
        LastUpdate = DateTime.UtcNow;
    }

    public bool SubmitCombatAction(CombatAction action)
    {
        if (Combat == null || Mode != GameMode.Combat) return false;

        var rng = new GameRandom(_encounterRng.Roll(1, 10000));
        Combat = CombatEngine.Tick(Combat, action, rng);

        // Auto-resolve AI turns
        while (!Combat.IsFinished && !(Combat.Phase == CombatPhase.Turn && Combat.CurrentActor?.IsPlayer == true))
        {
            Combat = CombatEngine.Tick(Combat, null, rng);
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
                    var updated = member with { CurrentHp = combatant.Hp, Xp = newXp };

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
            Party.SetMember(index, member with { CurrentHp = maxHp });
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
        InitializeDefaultParty();
        InitializeTown();
        ActionLog.Clear();
        _actionLogTurn = 0;
        _currentEncounterId = null;
        Reputation.Clear();
        SettingsHash = null;
        LastUpdate = DateTime.UtcNow;
    }

    private void EmitActionLog(string category, string type, Dictionary<string, string> payload)
    {
        _actionLogTurn++;
        ActionLog.Add(new ActionLogEntry(_actionLogTurn, category, type, new Dictionary<string, string>(payload)));
    }

    public void RestoreActionLog(List<ActionLogEntry> entries)
    {
        ActionLog.Clear();
        ActionLog.AddRange(entries);
        _actionLogTurn = entries.Count > 0 ? entries.Max(e => e.Turn) : 0;
    }

    public void SaveGame(string? path = null) => Save.SaveSystem.Save(this, path);
    public bool LoadGame(string? path = null) => Save.SaveSystem.Load(this, path);

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
        LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool PurchaseVendorItem(string itemId)
    {
        var item = Town.VendorStock.FirstOrDefault(v => v.ItemId == itemId);
        if (item == null) return false;

        Town.VendorStock.Remove(item);
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
