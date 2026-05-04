using RPC.Engine.Models.Dungeons;
using RPC.Engine.Dungeons;

namespace RPC.Engine;

public class GameState
{
    public Player Player { get; set; }
    public Dungeon? CurrentDungeon { get; set; }
    public GameMode Mode { get; set; } = GameMode.Exploration;
    public DateTime LastUpdate { get; set; }
    
    public GameState()
    {
        Player = new Player(new Position(32, 32), Direction.North);
        LastUpdate = DateTime.UtcNow;
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
