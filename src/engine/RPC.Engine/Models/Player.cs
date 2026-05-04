namespace RPC.Engine.Models.Dungeons;

public class Player
{
    public Position Position { get; set; }
    public Direction Facing { get; set; }
    
    public Player(Position position, Direction facing)
    {
        Position = position;
        Facing = facing;
    }

    public void MoveForward()
    {
        Position = Position.Move(Facing);
    }

    public void TurnLeft()
    {
        Facing = Facing.TurnLeft();
    }

    public void TurnRight()
    {
        Facing = Facing.TurnRight();
    }
}
