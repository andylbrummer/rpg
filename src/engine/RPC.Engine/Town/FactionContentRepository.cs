namespace RPC.Engine.Town;

public class FactionContentRepository
{
    private readonly List<FactionContentDef> _defs;

    public FactionContentRepository(List<FactionContentDef> defs)
    {
        _defs = defs ?? new List<FactionContentDef>();
    }

    public IReadOnlyList<FactionContentDef> Definitions => _defs;

    public List<FactionContact> GenerateContacts()
    {
        if (_defs.Count == 0)
        {
            return new List<FactionContact>
            {
                new("contact-bureau", "Agent Voss", "bureau", "portrait_voss"),
                new("contact-convocation", "Seer Maren", "convocation", "portrait_maren")
            };
        }

        return _defs.Select(d => new FactionContact(d.Contact.Id, d.Contact.Name, d.Id, d.Contact.Portrait)).ToList();
    }

    public List<MissionOffer> GenerateMissions()
    {
        if (_defs.Count == 0)
        {
            return new List<MissionOffer>
            {
                new("mission-bureau-1", "Cleanse the Sewers", "Eliminate the rat infestation beneath the Reach.", 1, new[] { "100g" }, 10, "bureau", MissionType.Side),
                new("mission-bureau-2", "Patrol the Walls", "Guard the outer perimeter for one night.", 1, new[] { "50g" }, 5, "bureau", MissionType.Side),
                new("mission-convocation-1", "Gather Bloom Samples", "Collect rare flora from the Hollow.", 1, new[] { "75g" }, 10, "convocation", MissionType.Side),
                new("mission-convocation-2", "Scout the Crypt", "Investigate whispering echoes.", 1, new[] { "60g" }, 5, "convocation", MissionType.Side)
            };
        }

        return _defs.SelectMany(d => d.Missions.Select(m =>
            new MissionOffer(m.Id, m.Title, m.Description, m.MinLevel, m.Rewards, m.RepReward, d.Id, m.Type)))
            .ToList();
    }

    public List<FactionVendor> GenerateVendors()
    {
        if (_defs.Count == 0)
        {
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

        return _defs.Select(d => new FactionVendor(
            d.Id,
            d.VendorName,
            d.RepThresholds.VendorAccess,
            d.VendorStock)).ToList();
    }
}
