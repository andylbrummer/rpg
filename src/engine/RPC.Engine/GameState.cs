using RPC.Engine.Character;
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
    
    public GameState()
    {
        Player = new Player(new Position(32, 32), Direction.North);
        LastUpdate = DateTime.UtcNow;
        InitializeDefaultParty();
    }

    private void InitializeDefaultParty()
    {
        Party.SetMember(0, new CharacterState(
            Guid.NewGuid(), "Kael", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            new[] { "bone_spear", "tithe_touch" }, 0));
        Party.SetMember(1, new CharacterState(
            Guid.NewGuid(), "Sera", "stillblade", 1, 0,
            new BaseStats(5, 5, 4, 3, 4), 14, Equipment.Empty,
            new[] { "rend", "silence_strike" }, 0));
        Party.SetMember(2, new CharacterState(
            Guid.NewGuid(), "Mira", "cauterist", 1, 0,
            new BaseStats(3, 5, 4, 5, 4), 14, Equipment.Empty,
            new[] { "cauterize", "scalpel_dance" }, 1));
        Party.SetMember(3, new CharacterState(
            Guid.NewGuid(), "Vex", "hollow", 1, 0,
            new BaseStats(4, 6, 3, 4, 4), 11, Equipment.Empty,
            new[] { "shiv", "smoke_bomb" }, 1));
    }

    public HashSet<string> ExploredTiles { get; } = new();

    public void EnterDungeon(Dungeon dungeon)
    {
        CurrentDungeon = dungeon;
        ExploredTiles.Clear();
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
        
        var newPos = Player.Position.Move(Player.Facing);
        if (CurrentDungeon.CanMoveTo(newPos))
        {
            Player.Position = newPos;
            ExploreAroundPlayer();
            LastUpdate = DateTime.UtcNow;
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
}

public enum GameMode
{
    Menu,
    Exploration,
    Combat,
    Dialog
}
