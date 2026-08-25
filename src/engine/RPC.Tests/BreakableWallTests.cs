using System.Linq;
using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Inventory;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

/// <summary>
/// T51b break action. A discovered breakable wall can be broken open: its border clears on both
/// adjacent tiles (movement becomes possible) and the action spends one in-dungeon turn, aging
/// carried bloom samples through the same dungeon-turn tick the bloom-decay system uses.
/// </summary>
public class BreakableWallTests
{
    private static GameState WallState(out Dungeon dungeon, Position player)
    {
        var gs = new GameState(seed: 1);
        dungeon = new Dungeon(6, 6, "test");
        for (int x = 0; x < 6; x++)
            for (int y = 0; y < 6; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
        // Breakable wall on the north border of (3,2) <-> south border of (3,1).
        dungeon.Tiles[3, 2] = new Tile(TileType.Floor, North: BorderType.BreakableWall);
        dungeon.Tiles[3, 1] = new Tile(TileType.Floor, South: BorderType.BreakableWall);
        gs.CurrentDungeon = dungeon;
        gs.CurrentDungeonType = "test";
        gs.Mode = GameMode.Exploration;
        gs.Player.Position = player;
        gs.Player.Facing = Direction.North;
        return gs;
    }

    private static void AddMemberWithBloomSample(GameState gs, int slot = 0) =>
        gs.Party.SetMember(slot, new CharacterState(
            Guid.NewGuid(), "Mapper", "inkblood", 1, 0,
            new BaseStats(4, 4, 4, 4, 4), 20, Equipment.Empty, Array.Empty<string>(), 0,
            ComponentInventory: new[] { new ComponentStack(BloomDecaySystem.BloomSampleItemId, 1, 99, 0, false) }));

    [Fact]
    public void Break_DiscoveredWall_OpensBorderBothSides_AndCostsOneDungeonTurn()
    {
        var gs = WallState(out var dungeon, new Position(3, 2));
        AddMemberWithBloomSample(gs);
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 2, Wall: "North"));
        Assert.True(gs.DiscoverSecret("breakable_wall", "crack", "search"));

        // Wall still blocks before the break.
        Assert.False(dungeon.CanMoveTo(new Position(3, 2), Direction.North));

        Assert.True(gs.BreakWall("crack"));

        Assert.Equal(BorderType.None, dungeon.Tiles[3, 2].North);
        Assert.Equal(BorderType.None, dungeon.Tiles[3, 1].South);
        Assert.True(dungeon.CanMoveTo(new Position(3, 2), Direction.North));

        // One in-dungeon turn elapsed: the carried bloom sample aged by one.
        Assert.Equal(1, gs.Party.Members[0].ComponentInventory[0].DungeonTurnsAlive);

        Assert.Contains(gs.ActionLog, e => e.Type == "wall_broken" && e.Payload["secretId"] == "crack");
    }

    [Fact]
    public void Break_UndiscoveredWall_Fails_AndLeavesBorderIntact()
    {
        var gs = WallState(out var dungeon, new Position(3, 2));
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 2, Wall: "North"));

        Assert.False(gs.BreakWall("crack")); // type not yet revealed
        Assert.Equal(BorderType.BreakableWall, dungeon.Tiles[3, 2].North);
    }

    [Fact]
    public void Break_NonBreakableSecretType_Fails()
    {
        var gs = WallState(out _, new Position(3, 2));
        gs.Secrets.Register(new SecretDef("vault", "sealed_vault", X: 3, Y: 2, Wall: "North"));
        Assert.True(gs.DiscoverSecret("sealed_vault", "vault", "manual"));

        Assert.False(gs.BreakWall("vault"));
    }

    [Fact]
    public void Break_UnknownSecretId_Fails()
    {
        var gs = WallState(out _, new Position(3, 2));
        Assert.False(gs.BreakWall("does-not-exist"));
    }

    [Fact]
    public void DemoContent_CartographerCrackedWall_LoadsAndIsBreakable()
    {
        var registry = new SecretRegistry();
        registry.LoadFromDirectory(System.IO.Path.Combine(FindContentRoot(), "secrets"));
        var secret = registry.Get("cartographer_cracked_wall");

        Assert.NotNull(secret);
        Assert.Equal("breakable_wall", secret!.Type);
        Assert.Equal(3, secret.X);
        Assert.Equal(2, secret.Y);
        Assert.Equal("North", secret.Wall);
    }

    private static string FindContentRoot()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "content");
            if (System.IO.Directory.Exists(System.IO.Path.Combine(candidate, "secrets")))
                return candidate;
            dir = dir.Parent;
        }
        throw new System.IO.DirectoryNotFoundException("content/secrets not found from " + System.AppContext.BaseDirectory);
    }
}
