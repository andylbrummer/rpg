namespace RPC.Engine.Combat;

public class GameRandom
{
    private readonly Random _rng;

    public GameRandom(int seed)
    {
        _rng = new Random(seed);
    }

    public int Roll(int min, int max) => _rng.Next(min, max + 1);

    public int Next(int max) => _rng.Next(max);

    public int NextInt() => _rng.Next();
}
