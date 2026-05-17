using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Exploration;

public class ExplorationState
{
    public Player Player { get; set; }
    public Dungeon? CurrentDungeon { get; set; }
    public string? CurrentDungeonType { get; set; }
    public BoundedTileSet ExploredTiles { get; private set; }
    public int StepsSinceEncounter { get; set; }
    public Position? PendingTaggedEncounterTile { get; set; }
    public string? CurrentEncounterId { get; set; }

    private readonly HashSet<string> _exploredTilesSet = new();
    private readonly Queue<string> _exploredTilesOrder = new();
    private const int MaxExploredTiles = 4096;

    public ExplorationState()
    {
        Player = new Player(new Position(32, 32), Direction.North);
        ExploredTiles = new BoundedTileSet(_exploredTilesSet, _exploredTilesOrder, MaxExploredTiles);
    }

    public void Reset()
    {
        CurrentDungeon = null;
        CurrentDungeonType = null;
        ExploredTiles.Clear();
        StepsSinceEncounter = 0;
        PendingTaggedEncounterTile = null;
        CurrentEncounterId = null;
        Player = new Player(new Position(32, 32), Direction.North);
    }
}
