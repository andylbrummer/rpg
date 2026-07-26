using System.Text.Json;
using RPC.Engine.Save.Migrations;

namespace RPC.Engine.Save;

public static class SaveSystem
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string SavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheReach", "save.json");

    public static void Save(GameState state, string? path = null, string? contentHash = null)
    {
        var fileIo = new SaveFileIO(path);
        var data = SaveBuilder.Build(state);
        data.ContentHash = contentHash;
        var json = fileIo.Serialize(data);
        fileIo.WriteAtomic(json);
    }

    public static bool Load(GameState state, string? path = null, string? expectedContentHash = null, Dungeons.IDungeonGenerator? dungeonGenerator = null)
    {
        var fileIo = new SaveFileIO(path);
        if (!fileIo.Exists())
            return false;

        try
        {
            var json = fileIo.ReadAllText();
            if (json == null) return false;

            var doc = JsonDocument.Parse(json);
            var schemaVersion = doc.RootElement.TryGetProperty("schemaVersion", out var svProp)
                ? svProp.GetInt32()
                : 0;

            var pipeline = SaveMigrationPipeline.CreateDefault(SaveBuilder.CurrentSchemaVersion);
            if (!pipeline.CanMigrate(schemaVersion))
            {
                var quarantinePath = fileIo.Quarantine();
                Console.Error.WriteLine(
                    $"Save file '{fileIo.SavePath}' has unsupported schema version {schemaVersion}. Quarantined to '{quarantinePath}'.");
                return false;
            }

            var migrated = pipeline.Migrate(doc, schemaVersion);
            var data = JsonSerializer.Deserialize<SaveData>(migrated, Options);
            if (data == null) return false;

            foreach (var warning in SaveCompatibility.CheckContentHash(SaveMetadata.From(data), expectedContentHash))
            {
                Console.WriteLine($"[Save] {warning}");
            }

            // Validate referenced content ids against the registries the running build already
            // carries (classes, dungeon templates, factions, schemes, complications). Warnings only —
            // an unresolved reference does not make the save unloadable, so it is surfaced rather
            // than quarantined.
            foreach (var warning in SaveCompatibility.CheckContentReferences(
                data, state.ClassContent, state.DungeonTemplates, state.FactionContent, state.CampaignContent))
            {
                Console.WriteLine($"[Save] {warning}");
            }

            SaveRestorer.RestoreParty(state, data);
            SaveRestorer.RestorePlayer(state, data);
            SaveRestorer.RestoreExploredTiles(state, data);
            SaveRestorer.RestoreCollectedLoot(state, data);
            SaveRestorer.RestoreMode(state, data);
            SaveRestorer.RestoreDungeonType(state, data);
            SaveRestorer.RestoreTown(state, data);
            SaveRestorer.RestoreActionLog(state, data);
            SaveRestorer.RestoreReputation(state, data);
            SaveRestorer.RestoreOverworld(state, data);
            SaveRestorer.RestoreSettings(state, data);
            SaveRestorer.RestoreJournal(state, data);
            SaveRestorer.RestoreHeat(state, data);
            SaveRestorer.RestoreCampaignConfig(state, data);
            SaveRestorer.RestoreEvidence(state, data);
            SaveRestorer.RestoreWorldState(state, data);
            SaveRestorer.RestoreDowntime(state, data);
            SaveRestorer.RestoreWildCardAlliance(state, data);
            SaveRestorer.RestoreStepsSinceEncounter(state, data);
            SaveRestorer.RestoreIronman(state, data);

            if (dungeonGenerator != null
                && state.Mode == GameMode.Exploration
                && state.CurrentDungeon == null
                && !string.IsNullOrEmpty(state.CurrentDungeonType))
            {
                // Reconstruct the dungeon from its persisted identity (type + effective seed +
                // content hash) so the same layout is reproduced deterministically on load.
                var request = new Dungeons.DungeonGenerationRequest(
                    state.CurrentDungeonType,
                    data.DungeonSeed != 0 ? data.DungeonSeed : null,
                    data.ContentHash);
                state.CurrentDungeon = dungeonGenerator.Generate(request).Dungeon;
            }

            return true;
        }
        catch (Exception ex)
        {
            // Set the file aside before returning. A save that parses but cannot be restored was
            // previously left in place, and the game carries on from a default state — in ironman
            // that means the next state-changing command autosaves straight over the player's
            // campaign, destroying a file that a later build (or a human) might have recovered.
            // Quarantining preserves it under a timestamped name and stops the same failure
            // repeating on every launch.
            var quarantinePath = TryQuarantine(fileIo);
            Console.Error.WriteLine(quarantinePath is null
                ? $"Failed to load save '{fileIo.SavePath}': {ex.Message}. The file could not be set aside; it is still in place."
                : $"Failed to load save '{fileIo.SavePath}': {ex.Message}. Moved to '{quarantinePath}' so it cannot be overwritten.");
            return false;
        }
    }

    /// <summary>
    /// Moves the unreadable save aside, returning the new path, or null if it could not be moved.
    /// Never throws: this runs while already handling a failure, and losing the original error to
    /// a secondary one would hide why the load failed in the first place.
    /// </summary>
    private static string? TryQuarantine(SaveFileIO fileIo)
    {
        try
        {
            var path = fileIo.Quarantine();
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Save] Could not quarantine '{fileIo.SavePath}': {ex.Message}");
            return null;
        }
    }

    public static bool HasSave(string? path = null)
    {
        return new SaveFileIO(path).Exists();
    }

}
