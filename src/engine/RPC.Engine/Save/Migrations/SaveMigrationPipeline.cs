using System.Text.Json;

namespace RPC.Engine.Save.Migrations;

public class SaveMigrationPipeline
{
    private readonly Dictionary<int, ISaveMigration> _migrations = new();
    public int TargetVersion { get; }

    public SaveMigrationPipeline(int targetVersion, IEnumerable<ISaveMigration> migrations)
    {
        TargetVersion = targetVersion;
        foreach (var migration in migrations.OrderBy(m => m.FromVersion))
        {
            _migrations[migration.FromVersion] = migration;
        }
    }

    /// <summary>
    /// Builds the supported migration chain (oldest supported version 3 → current).
    ///
    /// Every step is an <see cref="IdentityMigration"/> by design, not by omission: schema
    /// versions 3..11 only ever <em>added</em> back-compatible fields. No property was renamed,
    /// removed, or restructured across that range, and <see cref="SaveData"/> deserializes with
    /// System.Text.Json where every field has a default — so an older save simply omits the
    /// newer keys and they hydrate to their defaults. Bumping <c>schemaVersion</c> (what
    /// <see cref="IdentityMigration"/> does) is therefore the complete and correct transform for
    /// each step. Fabricating field-rewriting migrations here would be dead code.
    ///
    /// If a future schema version introduces a <em>breaking</em> shape change (a rename, a moved
    /// field, a changed array element shape), replace that step's <see cref="IdentityMigration"/>
    /// with a real <see cref="ISaveMigration"/> that maps the old shape onto the new one, and add
    /// a golden fixture for the pre-change version (see SaveGoldenFixtureTests).
    /// </summary>
    public static SaveMigrationPipeline CreateDefault(int targetVersion = 11)
    {
        var migrations = new List<ISaveMigration>();
        for (int v = 3; v < targetVersion; v++)
        {
            migrations.Add(new IdentityMigration(v, v + 1));
        }
        return new SaveMigrationPipeline(targetVersion, migrations);
    }

    public bool CanMigrate(int fromVersion)
    {
        if (fromVersion == TargetVersion) return true;
        if (fromVersion > TargetVersion) return false;

        var visited = new HashSet<int>();
        var current = fromVersion;
        while (current < TargetVersion)
        {
            if (!_migrations.ContainsKey(current)) return false;
            if (!visited.Add(current)) return false; // cycle detection
            current = _migrations[current].ToVersion;
        }
        return current == TargetVersion;
    }

    public JsonDocument Migrate(JsonDocument input, int fromVersion)
    {
        if (fromVersion == TargetVersion)
            return input;

        if (!CanMigrate(fromVersion))
            throw new InvalidOperationException($"Cannot migrate save from version {fromVersion} to {TargetVersion}");

        var current = input;
        var currentVersion = fromVersion;
        while (currentVersion < TargetVersion)
        {
            var migration = _migrations[currentVersion];
            current = migration.Migrate(current);
            currentVersion = migration.ToVersion;
        }

        return current;
    }
}
