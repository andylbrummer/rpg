using RPC.Engine;

namespace RPC.Host.Web.Presenters;

public static class TownPresenter
{
    public static object Present(GameState state)
    {
        return new
        {
            currentTownId = state.Town.CurrentTownId,
            availableMissions = state.Town.AvailableMissions.Select(m => new
            {
                id = m.Id,
                title = m.Title,
                description = m.Description,
                minLevel = m.MinLevel,
                rewards = m.Rewards,
                repReward = m.RepReward,
                factionId = m.FactionId
            }).ToArray(),
            vendorStock = state.Town.VendorStock.Select(v => new
            {
                itemId = v.ItemId,
                name = v.Name,
                price = v.Price,
                quantity = v.Quantity
            }).ToArray(),
            factionVendors = state.Town.FactionVendors.Select(fv => new
            {
                factionId = fv.FactionId,
                name = fv.Name,
                threshold = fv.Threshold,
                stock = fv.Stock.Select(v => new
                {
                    itemId = v.ItemId,
                    name = v.Name,
                    price = v.Price,
                    quantity = v.Quantity
                }).ToArray()
            }).ToArray(),
            factionContacts = state.Town.FactionContacts.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                factionId = c.FactionId,
                portrait = c.Portrait,
                attitude = state.Reputation.GetAttitudeTier(c.FactionId).ToString().ToLowerInvariant()
            }).ToArray(),
            tavernRoster = state.Town.TavernRoster.Select(r => new
            {
                id = r.Id,
                name = r.Name,
                classId = r.ClassId,
                level = r.Level,
                baseStats = new
                {
                    strength = r.BaseStats.Strength,
                    dexterity = r.BaseStats.Dexterity,
                    constitution = r.BaseStats.Constitution,
                    intelligence = r.BaseStats.Intelligence,
                    willpower = r.BaseStats.Willpower
                },
                cost = r.Cost
            }).ToArray(),
            viewedMissions = state.Town.ViewedMissions.ToArray(),
            questLog = state.Town.QuestLog.Select(q => new
            {
                id = q.Id,
                title = q.Title,
                description = q.Description,
                repReward = q.RepReward,
                factionId = q.FactionId,
                status = q.Status.ToString().ToLowerInvariant()
            }).ToArray(),
            rumors = state.Town.Rumors.Select(r => new
            {
                id = r.Id,
                text = r.Text,
                truthStatus = r.TruthStatus.ToString().ToLowerInvariant(),
                verified = r.Verified,
                verificationResult = r.VerificationResult,
                relatedContentId = r.RelatedContentId,
                relatedFactionId = r.RelatedFactionId
            }).ToArray()
        };
    }
}
