using RPC.Engine.Character;

namespace RPC.Engine.Town;

public class TownState
{
    public string CurrentTownId { get; set; } = "the_reach";
    public List<MissionOffer> AvailableMissions { get; set; } = new();
    public List<VendorItem> VendorStock { get; set; } = new();
    public List<FactionContact> FactionContacts { get; set; } = new();
    public List<TavernRecruit> TavernRoster { get; set; } = new();
    public List<string> ViewedMissions { get; set; } = new();
    public List<ActiveMission> QuestLog { get; set; } = new();
}

public record MissionOffer(string Id, string Title, string Description, int MinLevel, string[] Rewards, int RepReward = 0, string FactionId = "");

public record VendorItem(string ItemId, string Name, int Price, int Quantity);

public record TavernRecruit(string Id, string Name, string ClassId, int Level, BaseStats BaseStats, int Cost);

public record FactionContact(string Id, string Name, string FactionId, string Portrait);

public record ActiveMission(string Id, string Title, string Description, int RepReward, string FactionId, string Status);

public static class TavernRecruitGenerator
{
    private static readonly string[] Names = new[]
    {
        "Roran", "Elara", "Thorne", "Mira", "Kael", "Sera",
        "Vex", "Juno", "Darius", "Lyra", "Bran", "Nyx"
    };

    private static readonly string[] Classes = new[]
    {
        "bonewarden", "stillblade", "cauterist", "hollow"
    };

    private static readonly BaseStats[] BaseStatPresets = new[]
    {
        new BaseStats(4, 3, 5, 4, 4),
        new BaseStats(5, 5, 4, 3, 4),
        new BaseStats(3, 5, 4, 5, 4),
        new BaseStats(4, 6, 3, 4, 4),
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

public static class FactionContactGenerator
{
    public static List<FactionContact> GenerateContacts()
    {
        return new List<FactionContact>
        {
            new("contact-bureau", "Agent Voss", "bureau", "portrait_voss"),
            new("contact-convocation", "Seer Maren", "convocation", "portrait_maren")
        };
    }

    public static List<MissionOffer> GenerateMissions()
    {
        return new List<MissionOffer>
        {
            new("mission-bureau-1", "Cleanse the Sewers", "Eliminate the rat infestation beneath the Reach.", 1, new[] { "100g" }, 10, "bureau"),
            new("mission-bureau-2", "Patrol the Walls", "Guard the outer perimeter for one night.", 1, new[] { "50g" }, 5, "bureau"),
            new("mission-convocation-1", "Gather Bloom Samples", "Collect rare flora from the Hollow.", 1, new[] { "75g" }, 10, "convocation"),
            new("mission-convocation-2", "Scout the Crypt", "Investigate whispering echoes.", 1, new[] { "60g" }, 5, "convocation")
        };
    }
}
