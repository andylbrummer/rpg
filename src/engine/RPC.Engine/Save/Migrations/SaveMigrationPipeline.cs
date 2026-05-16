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

    public static SaveMigrationPipeline CreateDefault(int targetVersion = 8)
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
