using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Dungeons;

namespace RPC.Engine.Exploration;

public class ExplorationService
{
    private readonly EncounterTableRegistry? _encounterTables;
    private readonly ClassRegistry? _classRegistry;
    private readonly GameRandom _encounterRng;

    public ExplorationService(EncounterTableRegistry? encounterTables, ClassRegistry? classRegistry, GameRandom encounterRng)
    {
        _encounterTables = encounterTables;
        _classRegistry = classRegistry;
        _encounterRng = encounterRng;
    }

    public void EnterDungeon(GameState state, Dungeon dungeon, string dungeonType)
    {
        if (state.CampaignEnded) return;
        if (state.HasPendingBranchChoices) return;
        if (state.Heat.IsLockdown)
        {
            state.EmitActionLog("heat", "dungeon_blocked_lockdown", new Dictionary<string, string>
            {
                { "heat", state.Heat.Value.ToString() }
            });
            return;
        }
        state.CurrentDungeon = dungeon;
        state.CurrentDungeonType = dungeonType;
        state.ExploredTiles.Clear();
        state.StepsSinceEncounter = 0;
        state.PendingTaggedEncounterTile = null;
        state.Mode = GameMode.Exploration;
        state.EmitActionLog("dungeon", "dungeon_entered", new Dictionary<string, string> { { "dungeonType", dungeonType } });
        state.IncrementTurns(1);
        // Find entrance position
        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                if (dungeon.Tiles[x, y].Type == TileType.Floor)
                {
                    state.Player.Position = new Position(x, y);
                    state.Player.Facing = Direction.North;
                    ExploreAroundPlayer(state);
                    return;
                }
            }
        }
    }

    public void ExploreAroundPlayer(GameState state)
    {
        if (state.CurrentDungeon == null) return;
        var px = state.Player.Position.X;
        var py = state.Player.Position.Y;
        var viewRadius = 3;
        int newTiles = 0;

        for (int x = Math.Max(0, px - viewRadius); x < Math.Min(state.CurrentDungeon.Width, px + viewRadius + 1); x++)
        {
            for (int y = Math.Max(0, py - viewRadius); y < Math.Min(state.CurrentDungeon.Height, py + viewRadius + 1); y++)
            {
                var tile = state.CurrentDungeon.Tiles[x, y];
                if (tile.Type != TileType.Empty)
                {
                    if (state.ExploredTiles.Add($"{x},{y}"))
                        newTiles++;
                }
            }
        }

        if (newTiles > 0)
        {
            const int exploreXpPerTile = 5;
            var totalExploreXp = newTiles * exploreXpPerTile;
            for (int i = 0; i < state.Party.Members.Length; i++)
            {
                var member = state.Party.Members[i];
                if (member.Id == Guid.Empty) continue;
                var updated = member with { Xp = member.Xp + totalExploreXp };
                if (_classRegistry?.Get(member.ClassId) is { } classDef)
                {
                    updated = LevelingSystem.CheckAndApplyLevelUps(updated, classDef);
                }
                state.Party.SetMember(i, updated);
            }
        }
    }

    public bool TryMoveForward(GameState state) => ExecuteMove(state, state.Player.Facing);
    public bool TryMoveBack(GameState state) => ExecuteMove(state, state.Player.Facing.Opposite());
    public bool TryStrafeLeft(GameState state) => ExecuteMove(state, state.Player.Facing.StrafeLeft());
    public bool TryStrafeRight(GameState state) => ExecuteMove(state, state.Player.Facing.StrafeRight());

    private bool ExecuteMove(GameState state, Direction dir)
    {
        if (state.CurrentDungeon == null) return false;
        if (state.Mode == GameMode.Combat) return false;

        var newPos = state.Player.Position.Move(dir);
        if (state.CurrentDungeon.CanMoveTo(state.Player.Position, dir))
        {
            state.Player.Position = newPos;
            ExploreAroundPlayer(state);
            state.LastUpdate = DateTime.UtcNow;
            state.StepsSinceEncounter++;

            var tile = state.CurrentDungeon.GetTile(newPos);
            if (!string.IsNullOrEmpty(tile.EncounterId))
            {
                var encounter = _encounterTables?.GetEncounterById(tile.EncounterId);
                if (encounter != null)
                {
                    state.PendingTaggedEncounterTile = newPos;
                    state.TriggerEncounter(encounter);
                    return true;
                }
            }

            var encounterChance = 0.05 + (state.StepsSinceEncounter * 0.08);
            if (_encounterRng.Roll(0, 99) < encounterChance * 100)
            {
                state.TriggerEncounter();
            }

            return true;
        }
        return false;
    }

    public void TurnLeft(GameState state)
    {
        state.Player.TurnLeft();
        state.LastUpdate = DateTime.UtcNow;
    }

    public void TurnRight(GameState state)
    {
        state.Player.TurnRight();
        state.LastUpdate = DateTime.UtcNow;
    }
}
