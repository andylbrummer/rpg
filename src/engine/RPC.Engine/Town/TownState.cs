using RPC.Engine.Character;

namespace RPC.Engine.Town;

public class TownState
{
    public string CurrentTownId { get; set; } = "the_reach";
    public List<MissionOffer> AvailableMissions { get; set; } = new();
    public List<VendorItem> VendorStock { get; set; } = new();
    public List<string> FactionContacts { get; set; } = new();
    public List<TavernRecruit> TavernRoster { get; set; } = new();
    public List<string> ViewedMissions { get; set; } = new();
}

public record MissionOffer(string Id, string Title, string Description, int MinLevel, string[] Rewards);

public record VendorItem(string ItemId, string Name, int Price, int Quantity);

public record TavernRecruit(string Id, string Name, string ClassId, int Level, BaseStats BaseStats, int Cost);

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
