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

    public void EnterDungeon(Dungeon dungeon)
    {
        CurrentDungeon = dungeon;
        // Find entrance position
        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                if (dungeon.Tiles[x, y].Type == TileType.Floor)
                {
                    Player.Position = new Position(x, y);
                    Player.Facing = Direction.North;
                    return;
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
