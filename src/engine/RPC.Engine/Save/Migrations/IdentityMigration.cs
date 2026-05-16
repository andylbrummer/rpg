using System.Text.Json;

namespace RPC.Engine.Save.Migrations;

public class IdentityMigration : ISaveMigration
{
    public int FromVersion { get; }
    public int ToVersion { get; }

    public IdentityMigration(int fromVersion, int toVersion)
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
    }

    public JsonDocument Migrate(JsonDocument input)
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        input.WriteTo(writer);
        writer.Flush();
        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var builder = new System.Text.Json.Nodes.JsonObject();
        foreach (var property in root.EnumerateObject())
        {
            if (property.NameEquals("schemaVersion"))
            {
                builder.Add("schemaVersion", ToVersion);
            }
            else
            {
                builder.Add(property.Name, System.Text.Json.Nodes.JsonNode.Parse(property.Value.GetRawText()));
            }
        }

        return JsonDocument.Parse(builder.ToJsonString());
    }
}
