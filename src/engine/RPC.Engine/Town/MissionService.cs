using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Town;

namespace RPC.Engine.Town;

public class MissionService
{
    private readonly ClassRegistry? _classRegistry;

    public MissionService(ClassRegistry? classRegistry)
    {
        _classRegistry = classRegistry;
    }

    public bool AcceptMission(GameState state, string missionId)
    {
        var mission = state.Town.AvailableMissions.FirstOrDefault(m => m.Id == missionId);
        if (mission == null) return false;

        state.Town.AvailableMissions.Remove(mission);
        state.Town.QuestLog.Add(new ActiveMission(mission.Id, mission.Title, mission.Description, mission.RepReward, mission.FactionId, MissionStatus.Active, mission.Type));
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool CompleteMission(GameState state, string missionId)
    {
        var mission = state.Town.QuestLog.FirstOrDefault(m => m.Id == missionId && m.Status == MissionStatus.Active);
        if (mission == null) return false;

        var index = state.Town.QuestLog.FindIndex(m => m.Id == missionId);
        state.Town.QuestLog[index] = mission with { Status = MissionStatus.Completed };

        var (primaryDelta, opposedDelta) = mission.Type switch
        {
            MissionType.Main => (8, -4),
            _ => (5, -2),
        };

        var vendorThreshold = state.Town.FactionVendors.FirstOrDefault(v => v.FactionId == mission.FactionId)?.Threshold ?? 25;
        var oldPrimaryRep = state.Reputation[mission.FactionId];
        var wasUnlocked = oldPrimaryRep >= vendorThreshold;

        var source = $"mission_complete_{mission.Type.ToString().ToLower()}";
        ApplyMissionReputation(state, mission.FactionId, primaryDelta, opposedDelta, source);

        // Grant quest completion XP
        var questXp = mission.Type == MissionType.Main ? 100 : 50;
        for (int i = 0; i < state.Party.Members.Length; i++)
        {
            var member = state.Party.Members[i];
            if (member.Id == Guid.Empty) continue;
            var updated = member with { Xp = member.Xp + questXp };
            if (_classRegistry?.Get(member.ClassId) is { } classDef)
            {
                updated = LevelingSystem.CheckAndApplyLevelUps(updated, classDef);
            }
            state.Party.SetMember(i, updated);
        }

        state.EmitActionLog("faction", "mission_completed", new Dictionary<string, string>
        {
            { "missionId", mission.Id },
            { "factionId", mission.FactionId },
            { "type", mission.Type.ToString().ToLower() },
            { "xpReward", questXp.ToString() }
        });

        var newPrimaryRep = state.Reputation[mission.FactionId];
        if (!wasUnlocked && newPrimaryRep >= vendorThreshold)
        {
            state.EmitActionLog("faction", "vendor_unlocked", new Dictionary<string, string>
            {
                { "factionId", mission.FactionId },
                { "threshold", vendorThreshold.ToString() }
            });
        }

        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool FailMission(GameState state, string missionId)
    {
        var mission = state.Town.QuestLog.FirstOrDefault(m => m.Id == missionId && m.Status == MissionStatus.Active);
        if (mission == null) return false;

        var index = state.Town.QuestLog.FindIndex(m => m.Id == missionId);
        state.Town.QuestLog[index] = mission with { Status = MissionStatus.Failed };

        ApplyMissionReputation(state, mission.FactionId, -3, 1, "mission_failed");

        state.EmitActionLog("faction", "mission_failed", new Dictionary<string, string>
        {
            { "missionId", mission.Id },
            { "factionId", mission.FactionId }
        });

        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public bool AbandonMission(GameState state, string missionId)
    {
        var mission = state.Town.QuestLog.FirstOrDefault(m => m.Id == missionId && m.Status == MissionStatus.Active);
        if (mission == null) return false;

        var index = state.Town.QuestLog.FindIndex(m => m.Id == missionId);
        state.Town.QuestLog[index] = mission with { Status = MissionStatus.Abandoned };

        ApplyMissionReputation(state, mission.FactionId, -3, 1, "mission_abandoned");
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    private static void ApplyMissionReputation(GameState state, string factionId, int primaryDelta, int opposedDelta, string source)
    {
        var primaryChanges = state.Reputation.ApplyDelta(factionId, primaryDelta, source, propagate: false);
        foreach (var change in primaryChanges)
        {
            state.EmitActionLog("faction", "rep_changed", new Dictionary<string, string>
            {
                { "factionId", change.FactionId },
                { "delta", change.Delta.ToString() },
                { "newValue", change.NewValue.ToString() },
                { "source", change.Source }
            });
        }

        if (opposedDelta != 0)
        {
            var opposedFactionId = state.Reputation.GetOpposedFaction(factionId);
            if (opposedFactionId != null)
            {
                var opposedChanges = state.Reputation.ApplyDelta(opposedFactionId, opposedDelta, source, propagate: false);
                foreach (var change in opposedChanges)
                {
                    state.EmitActionLog("faction", "rep_changed", new Dictionary<string, string>
                    {
                        { "factionId", change.FactionId },
                        { "delta", change.Delta.ToString() },
                        { "newValue", change.NewValue.ToString() },
                        { "source", change.Source }
                    });
                }
            }
        }
    }
}
