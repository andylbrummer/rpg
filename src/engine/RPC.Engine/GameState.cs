using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Dungeons;
using RPC.Engine.Party;

namespace RPC.Engine;

public class GameState
{
    public Player Player { get; set; }
    public Dungeon? CurrentDungeon { get; set; }
    public GameMode Mode { get; set; } = GameMode.Exploration;
    public DateTime LastUpdate { get; set; }
    public PartyState Party { get; set; } = new();
    public CombatState? Combat { get; private set; }
    public CombatResult? LastCombatResult { get; private set; }
    public List<CombatLogEntry> CombatLog => Combat?.Log ?? new List<CombatLogEntry>();

    public void ClearCombatResult()
    {
        LastCombatResult = null;
    }

    private readonly GameRandom _encounterRng;
    private readonly EncounterTableRegistry? _encounterTables;
    private int _stepsSinceEncounter = 0;

    public GameState(int? seed = null, EncounterTableRegistry? encounterTables = null)
    {
        Player = new Player(new Position(32, 32), Direction.North);
        LastUpdate = DateTime.UtcNow;
        _encounterRng = new GameRandom(seed ?? DateTime.UtcNow.GetHashCode());
        _encounterTables = encounterTables;
        InitializeDefaultParty();
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

    public HashSet<string> ExploredTiles { get; } = new();

    public void EnterDungeon(Dungeon dungeon)
    {
        CurrentDungeon = dungeon;
        ExploredTiles.Clear();
        _stepsSinceEncounter = 0;
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
        if (CurrentDungeon.CanMoveTo(newPos))
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

        encounter ??= new EncounterDef("random", "Random Encounter", new[]
        {
            new EnemySpawn("rat", _encounterRng.Roll(1, 2)),
            new EnemySpawn("goblin_scavenger", _encounterRng.Roll(0, 1))
        });

        Combat = CombatEngine.Enter(Party, encounter, new GameRandom(_encounterRng.Roll(1, 10000)));

        if (Combat.IsFinished)
        {
            Mode = GameMode.Exploration;
            Combat = null;
        }
        else
        {
            Mode = GameMode.Combat;
        }
        LastUpdate = DateTime.UtcNow;
    }

    public bool SubmitCombatAction(CombatAction action)
    {
        if (Combat == null || Mode != GameMode.Combat) return false;

        var rng = new GameRandom(_encounterRng.Roll(1, 10000));
        Combat = CombatEngine.Tick(Combat, action, rng);

        // Auto-resolve AI turns
        while (Combat.Phase == CombatPhase.Turn && Combat.CurrentActor?.IsPlayer == false && !Combat.IsFinished)
        {
            Combat = CombatEngine.Tick(Combat, null, rng);
        }

        if (Combat.IsFinished)
        {
            // Apply combat results to party
            var levelUps = new List<string>();
            foreach (var combatant in Combat.Combatants.Where(c => c.IsPlayer))
            {
                var member = Party.Members.FirstOrDefault(m => m.Id == combatant.Id);
                if (member.Id != Guid.Empty)
                {
                    var index = Array.IndexOf(Party.Members, member);
                    var newXp = member.Xp + Combat.XpReward;
                    Party.SetMember(index,
                        member with { CurrentHp = combatant.Hp, Xp = newXp });
                }
            }

            LastCombatResult = new CombatResult(
                Combat.AllEnemiesDead,
                Combat.XpReward,
                levelUps.ToArray(),
                Combat.Round);

            Mode = GameMode.Exploration;
            Combat = null;
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
        Mode = GameMode.Menu;
        CurrentDungeon = null;
        LastUpdate = DateTime.UtcNow;
    }
}

public enum GameMode
{
    Menu,
    Exploration,
    Combat,
    Dialog
}
