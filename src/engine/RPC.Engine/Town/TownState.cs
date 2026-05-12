using System.Text.Json;
using RPC.Engine.Character;

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
}

public record VendorItem(string ItemId, string Name, int Price, int Quantity);

public record FactionVendor(string FactionId, string Name, int Threshold, List<VendorItem> Stock);

public record TavernRecruit(string Id, string Name, string ClassId, int Level, BaseStats BaseStats, int Cost);

public enum MissionType { Side, Main }

public record FactionContact(string Id, string Name, string FactionId, string Portrait);

public record ActiveMission(string Id, string Title, string Description, int RepReward, string FactionId, string Status, MissionType Type = MissionType.Side);

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
        "bonewarden", "stillblade", "cauterist", "hollow", "fieldwright", "inkblood"
    };

    private static readonly BaseStats[] BaseStatPresets = new[]
    {
        new BaseStats(4, 3, 5, 4, 4),
        new BaseStats(5, 5, 4, 3, 4),
        new BaseStats(3, 5, 4, 5, 4),
        new BaseStats(4, 6, 3, 4, 4),
        new BaseStats(3, 4, 5, 6, 3),
        new BaseStats(3, 4, 4, 5, 6),
    };

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
}

public static class FactionContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true
    };

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

public static class FactionContactGenerator
{
    private static List<FactionContentDef>? _loaded;

    public static void SetContent(List<FactionContentDef> defs) => _loaded = defs;

    public static List<FactionContact> GenerateContacts()
    {
        if (_loaded != null)
        {
            return _loaded.Select(d => new FactionContact(d.Contact.Id, d.Contact.Name, d.Id, d.Contact.Portrait)).ToList();
        }

        return new List<FactionContact>
        {
            new("contact-bureau", "Agent Voss", "bureau", "portrait_voss"),
            new("contact-convocation", "Seer Maren", "convocation", "portrait_maren")
        };
    }

    public static List<MissionOffer> GenerateMissions()
    {
        if (_loaded != null)
        {
            return _loaded.SelectMany(d => d.Missions.Select(m =>
                new MissionOffer(m.Id, m.Title, m.Description, m.MinLevel, m.Rewards, m.RepReward, d.Id, m.Type)))
                .ToList();
        }

        return new List<MissionOffer>
        {
            new("mission-bureau-1", "Cleanse the Sewers", "Eliminate the rat infestation beneath the Reach.", 1, new[] { "100g" }, 10, "bureau", MissionType.Side),
            new("mission-bureau-2", "Patrol the Walls", "Guard the outer perimeter for one night.", 1, new[] { "50g" }, 5, "bureau", MissionType.Side),
            new("mission-convocation-1", "Gather Bloom Samples", "Collect rare flora from the Hollow.", 1, new[] { "75g" }, 10, "convocation", MissionType.Side),
            new("mission-convocation-2", "Scout the Crypt", "Investigate whispering echoes.", 1, new[] { "60g" }, 5, "convocation", MissionType.Side)
        };
    }
}

public static class FactionVendorGenerator
{
    private static List<FactionContentDef>? _loaded;

    public static void SetContent(List<FactionContentDef> defs) => _loaded = defs;

    public static List<FactionVendor> GenerateStock()
    {
        if (_loaded != null)
        {
            return _loaded.Select(d => new FactionVendor(
                d.Id,
                d.VendorName,
                d.RepThresholds.VendorAccess,
                d.VendorStock)).ToList();
        }

        return new List<FactionVendor>
        {
            new("bureau", "Bureau Quartermaster", 25, new List<VendorItem>
            {
                new("healing_draft", "Healing Draft", 35, 3),
                new("iron_mace", "Iron Mace", 25, 1),
                new("antitoxin", "Antitoxin", 25, 2)
            }),
            new("convocation", "Convocation Arcanist", 25, new List<VendorItem>
            {
                new("small_salve", "Small Salve", 15, 5),
                new("cautery_knife", "Cautery Knife", 40, 1),
                new("clear_mind", "Clear Mind", 20, 2)
            })
        };
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
