using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

public interface IDungeonGenerator
{
    Dungeon Generate(string dungeonType, int? seed = null);
}
