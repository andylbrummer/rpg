using System.Text.Json;

namespace RPC.Engine.Save.Migrations;

public interface ISaveMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    JsonDocument Migrate(JsonDocument input);
}
