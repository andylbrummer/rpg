using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Save;

namespace RPC.Tests;

/// <summary>
/// Golden-save fixtures for every supported schema version (3..9). Each fixture is a save
/// authored at that version; the tests assert it migrates to the current schema and restores a
/// coherent <see cref="GameState"/>. This is the regression guard against a future schema change
/// silently breaking older saves — when a breaking change lands, the matching fixture fails here.
/// </summary>
public class SaveGoldenFixtureTests : IDisposable
{
    private const int OldestSupportedVersion = 3;
    private const int CurrentVersion = 12;

    private readonly string _workPath;

    public SaveGoldenFixtureTests()
    {
        _workPath = Path.Combine(Path.GetTempPath(), $"golden_fixture_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_workPath)) File.Delete(_workPath);
        if (File.Exists(_workPath + ".tmp")) File.Delete(_workPath + ".tmp");
    }

    private static string FixturePath(int version) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "golden-saves", $"v{version}.json");

    public static IEnumerable<object[]> SupportedVersions()
    {
        for (int v = OldestSupportedVersion; v <= CurrentVersion; v++)
            yield return new object[] { v };
    }

    [Theory]
    [MemberData(nameof(SupportedVersions))]
    public void GoldenFixture_MigratesAndRestoresCoherentState(int version)
    {
        var fixturePath = FixturePath(version);
        Assert.True(File.Exists(fixturePath), $"Missing golden fixture for schema v{version}: {fixturePath}");

        // Copy into a private working path so loading never mutates the committed fixture.
        File.Copy(fixturePath, _workPath, overwrite: true);

        var gs = new GameState(seed: 1);
        var loaded = gs.LoadGame(_workPath);

        Assert.True(loaded, $"v{version} fixture failed to load/migrate");

        // Shared core present in every fixture — the coherent restored state contract.
        Assert.Equal(GameMode.Menu, gs.Mode);
        Assert.Equal(2, gs.Player.Position.X);
        Assert.Equal(3, gs.Player.Position.Y);
        Assert.Equal(Direction.East, gs.Player.Facing);
        Assert.Equal(15, gs.Reputation["bureau"]);
        Assert.Equal("bonewarden", gs.Party.Members[0].ClassId);
        Assert.Equal(3, gs.Party.Members[0].Level);
        Assert.Equal("crypt", gs.CurrentDungeonType);
    }

    [Theory]
    [MemberData(nameof(SupportedVersions))]
    public void GoldenFixture_ReSavesAtCurrentSchemaVersion(int version)
    {
        File.Copy(FixturePath(version), _workPath, overwrite: true);

        var gs = new GameState(seed: 1);
        Assert.True(gs.LoadGame(_workPath));

        gs.SaveGame(_workPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(_workPath));
        Assert.Equal(CurrentVersion, doc.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void GoldenFixture_V7_RestoresAdditiveCollectedLoot()
    {
        File.Copy(FixturePath(7), _workPath, overwrite: true);

        var gs = new GameState(seed: 1);
        Assert.True(gs.LoadGame(_workPath));

        Assert.Contains("chest_a1", gs.Exploration.CollectedLoot);
        Assert.Contains("altar_b2", gs.Exploration.CollectedLoot);
    }

    [Fact]
    public void GoldenFixture_V8_RestoresIronmanFlag()
    {
        File.Copy(FixturePath(8), _workPath, overwrite: true);

        var gs = new GameState(seed: 1);
        Assert.True(gs.LoadGame(_workPath));

        Assert.True(gs.IsIronman);
    }

    [Fact]
    public void GoldenFixture_V9_RestoresBenchedCharacter()
    {
        File.Copy(FixturePath(9), _workPath, overwrite: true);

        var gs = new GameState(seed: 1);
        Assert.True(gs.LoadGame(_workPath));

        var benched = Assert.Single(gs.Party.Bench);
        Assert.Equal("Mira", benched.Name);
        Assert.Equal("stillblade", benched.ClassId);
        Assert.Equal(2, benched.Level);
    }

    [Fact]
    public void GoldenFixture_V10_RestoresTitheDebt()
    {
        File.Copy(FixturePath(10), _workPath, overwrite: true);

        var gs = new GameState(seed: 1);
        Assert.True(gs.LoadGame(_workPath));

        Assert.Equal(2, gs.Tithe.Debt);
        Assert.True(gs.Tithe.HasDebt);
        Assert.Equal(1, gs.Tithe.OutstandingSinceTurn);
        Assert.Contains(1, gs.Tithe.BilledMilestones);
    }

    [Fact]
    public void GoldenFixture_V11_RestoresBloomSampleDecayCounters()
    {
        File.Copy(FixturePath(11), _workPath, overwrite: true);

        var gs = new GameState(seed: 1);
        Assert.True(gs.LoadGame(_workPath));

        var samples = gs.Party.Members[0].ComponentInventory
            .Where(s => s.ItemId == "bloom_sample")
            .OrderByDescending(s => s.DungeonTurnsAlive)
            .ToArray();

        Assert.Equal(2, samples.Length);
        Assert.Equal(6, samples[0].DungeonTurnsAlive);
        Assert.False(samples[0].Stabilized);
        Assert.Equal(3, samples[1].DungeonTurnsAlive);
        Assert.True(samples[1].Stabilized);
    }

    [Fact]
    public void GoldenFixture_V12_RestoresFamilyName()
    {
        File.Copy(FixturePath(12), _workPath, overwrite: true);

        var gs = new GameState(seed: 1);
        Assert.True(gs.LoadGame(_workPath));

        Assert.Equal("Thornwick", gs.Campaign.FamilyName);
    }
}
