using System.Text.Json;
using RPC.Engine.Character;
using RPC.Engine.Content;

namespace RPC.Engine.Town;

public class TownState
{
    public string CurrentTownId { get; set; } = "the_reach";
    public List<MissionOffer> AvailableMissions { get; set; } = new();
    public List<VendorItem> VendorStock { get; set; } = new();
    public List<FactionVendor> FactionVendors { get; set; } = new();
    public List<FactionContact> FactionContacts { get; set; } = new();
    public List<TavernRecruit> TavernRoster { get; set; } = new();
    public List<string> ViewedMissions { get; set; } = new();
    public List<ActiveMission> QuestLog { get; set; } = new();
    public List<TownRumor> Rumors { get; set; } = new();
}

public record VendorItem(string ItemId, string Name, int Price, int Quantity);

public record FactionVendor(string FactionId, string Name, int Threshold, List<VendorItem> Stock);

public record TavernRecruit(string Id, string Name, string ClassId, int Level, BaseStats BaseStats, int Cost);

public enum MissionType { Side, Main }

public enum MissionStatus { Active, Completed, Failed, Abandoned }

public record FactionContact(string Id, string Name, string FactionId, string Portrait);

public record ActiveMission(string Id, string Title, string Description, int RepReward, string FactionId, MissionStatus Status, MissionType Type = MissionType.Side);

public record MissionOffer(string Id, string Title, string Description, int MinLevel, string[] Rewards, int RepReward = 0, string FactionId = "", MissionType Type = MissionType.Side);

public record FactionContentDef(
    string Id,
    string Name,
    string ShortName,
    FactionContactDef Contact,
    string VendorName,
    string Identity,
    string ColorPrimary,
    int StarterRep,
    RepThresholdsDef RepThresholds,
    List<VendorItem> VendorStock,
    List<FactionMissionDef> Missions);

public record FactionContactDef(string Id, string Name, string Portrait);
public record RepThresholdsDef(int VendorAccess, int ExclusiveRecruit, int PatronOffice);
public record FactionMissionDef(string Id, string Title, string Description, int MinLevel, string[] Rewards, int RepReward, MissionType Type = MissionType.Side);

public static class TavernRecruitGenerator
{
    private static readonly string[] Names = new[]
    {
        "Roran", "Elara", "Thorne", "Mira", "Kael", "Sera",
        "Vex", "Juno", "Darius", "Lyra", "Bran", "Nyx"
    };

    private static readonly string[] Classes = new[]
    {
        "bonewarden", "stillblade", "cauterist", "hollow", "fieldwright", "inkblood", "marcher", "ashmouth"
    };

    private static readonly BaseStats[] BaseStatPresets = new[]
    {
        new BaseStats(4, 3, 5, 4, 4),
        new BaseStats(5, 5, 4, 3, 4),
        new BaseStats(3, 5, 4, 5, 4),
        new BaseStats(4, 6, 3, 4, 4),
        new BaseStats(3, 4, 5, 6, 3),
        new BaseStats(3, 4, 4, 5, 6),
        new BaseStats(3, 6, 3, 4, 5),
        new BaseStats(5, 4, 5, 3, 4),
    };

    // Faction-locked recruit archetypes. Each appears in the tavern only once the player's
    // reputation with the gating faction reaches the threshold ("Compact" is the inkblood
    // Ossuary Compact). These are the alternate path to the faction-locked class branches.
    public record ExclusiveRecruitDef(string ClassId, string Name, string FactionId, int RepThreshold, int Level, BaseStats BaseStats);

    private static readonly ExclusiveRecruitDef[] ExclusiveRecruits = new[]
    {
        new ExclusiveRecruitDef("beastkeeper", "Aldric the Tamer", "inkblood", 25, 3, new BaseStats(4, 4, 5, 4, 5)),
        new ExclusiveRecruitDef("heretic", "Sister Vael", "convocation", 25, 3, new BaseStats(3, 4, 4, 6, 5)),
        new ExclusiveRecruitDef("liar", "Smiling Cassius", "bureau", 20, 2, new BaseStats(3, 5, 4, 5, 5)),
    };

    public static IReadOnlyList<ExclusiveRecruitDef> ExclusiveRecruitDefs => ExclusiveRecruits;

    public const string ExclusiveIdPrefix = "recruit-exclusive-";

    public static List<TavernRecruit> GenerateRoster(int seed)
    {
        var rng = new Random(seed);
        var roster = new List<TavernRecruit>(6);
        var usedNames = new HashSet<string>();

        for (int i = 0; i < 6; i++)
        {
            string name;
            do
            {
                name = Names[rng.Next(Names.Length)];
            } while (!usedNames.Add(name));

            var classIndex = rng.Next(Classes.Length);
            var classId = Classes[classIndex];
            var level = rng.Next(1, 4);
            var cost = level * 50 + rng.Next(0, 25);

            roster.Add(new TavernRecruit(
                Id: $"recruit-{seed % 100000:D5}-{i}",
                Name: name,
                ClassId: classId,
                Level: level,
                BaseStats: BaseStatPresets[classIndex],
                Cost: cost));
        }

        return roster;
    }

    /// <summary>The faction-exclusive recruits the player currently qualifies for, by reputation.</summary>
    public static List<TavernRecruit> GetExclusiveRecruits(ReputationState reputation)
    {
        var list = new List<TavernRecruit>();
        foreach (var ex in ExclusiveRecruits)
        {
            if (reputation[ex.FactionId] >= ex.RepThreshold)
            {
                list.Add(new TavernRecruit(
                    Id: $"{ExclusiveIdPrefix}{ex.ClassId}",
                    Name: ex.Name,
                    ClassId: ex.ClassId,
                    Level: ex.Level,
                    BaseStats: ex.BaseStats,
                    Cost: ex.Level * 60 + 50));
            }
        }
        return list;
    }

    /// <summary>Standard roster plus any faction-exclusive recruits unlocked by reputation.</summary>
    public static List<TavernRecruit> GenerateRoster(int seed, ReputationState reputation)
    {
        var roster = GenerateRoster(seed);
        roster.AddRange(GetExclusiveRecruits(reputation));
        return roster;
    }
}

public static class FactionContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = ContentJsonOptions.Standard;

    /// <summary>
    /// Catalog-driven faction load. Used by the host composition root (which picks the pack/loose
    /// <see cref="IContentCatalog"/>) so the engine never infers a content directory off the
    /// filesystem on the production path.
    /// </summary>
    public static List<FactionContentDef> LoadAll(IContentCatalog catalog)
    {
        var defs = new List<FactionContentDef>();
        foreach (var file in catalog.EnumerateFiles("factions", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"factions/{Path.GetFileName(file)}");
            if (json == null)
                continue;
            var def = JsonSerializer.Deserialize<FactionContentDef>(json, JsonOptions);
            if (def != null)
                defs.Add(def);
        }
        return defs;
    }

    /// <summary>
    /// Loads faction defs from loose content on disk. Used only as a fallback by engine tests that
    /// call this directly; the host always loads through <see cref="LoadAll(IContentCatalog)"/>.
    /// </summary>
    public static List<FactionContentDef> LoadAll(string? contentDir = null)
    {
        var dir = contentDir ?? FindContentDir();
        if (dir == null || !Directory.Exists(dir))
            return new List<FactionContentDef>();

        var defs = new List<FactionContentDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f))
        {
            var json = File.ReadAllText(file);
            var def = JsonSerializer.Deserialize<FactionContentDef>(json, JsonOptions);
            if (def != null)
                defs.Add(def);
        }
        return defs;
    }

    private static string? FindContentDir()
    {
        var baseDir = AppContext.BaseDirectory;
        for (int ups = 0; ups <= 8; ups++)
        {
            var parts = new List<string> { baseDir };
            for (int i = 0; i < ups; i++) parts.Add("..");
            parts.AddRange(new[] { "content", "factions" });
            var candidate = Path.GetFullPath(Path.Combine(parts.ToArray()));
            if (Directory.Exists(candidate))
                return candidate;
        }
        return null;
    }
}

public static class VendorFilter
{
    public static List<FactionVendor> GetAvailableVendors(TownState town, ReputationState reputation)
    {
        return town.FactionVendors
            .Where(v => reputation[v.FactionId] >= v.Threshold)
            .ToList();
    }

    public static bool IsVendorVisible(string factionId, ReputationState reputation)
    {
        return reputation[factionId] > -25;
    }
}
