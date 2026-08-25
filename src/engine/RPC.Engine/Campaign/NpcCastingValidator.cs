using RPC.Engine.Combat;

namespace RPC.Engine.Campaign;

/// <summary>A problem found in an NPC casting map (npcId -> role).</summary>
public record CastingIssue(string Kind, string Role, string NpcId, string Detail);

/// <summary>
/// Validates and repairs the NPC casting produced for a generated campaign. Catches role conflicts
/// (a role cast to more than one NPC), missing required roles, and faction inconsistency (an NPC
/// whose faction does not match the campaign faction expected to supply its role). Repair re-casts
/// required roles from a candidate pool, avoiding double-casting the same NPC.
/// </summary>
public static class NpcCastingValidator
{
    /// <summary>Roles every generated campaign must cast.</summary>
    public static readonly string[] RequiredRoles = { "questGiver", "boss" };

    /// <summary>The campaign faction expected to supply a given role, or null if unconstrained.</summary>
    public static string? ExpectedFaction(string role, CampaignConfig config) => role switch
    {
        "questGiver" or "patron_contact" => config.Patron,
        "boss" or "mastermind" => config.Mastermind,
        "threat_lieutenant" => config.Threat,
        "wildcard_agent" => config.WildCard,
        _ => null,
    };

    /// <param name="casting">npcId -> role.</param>
    /// <param name="npcFactions">npcId -> faction. When null, faction consistency is not checked.</param>
    public static List<CastingIssue> Validate(
        IReadOnlyDictionary<string, string> casting,
        CampaignConfig config,
        IReadOnlyDictionary<string, string>? npcFactions = null,
        IReadOnlyCollection<string>? requiredRoles = null)
    {
        requiredRoles ??= RequiredRoles;
        var issues = new List<CastingIssue>();

        // Group NPCs by role. (The dict keys are npc ids, so one NPC cannot hold two roles; the
        // realistic generation error is a role filled by multiple NPCs.)
        var roleToNpcs = new Dictionary<string, List<string>>();
        foreach (var (npc, role) in casting)
        {
            if (string.IsNullOrWhiteSpace(npc) || string.IsNullOrWhiteSpace(role))
            {
                issues.Add(new CastingIssue("empty", role ?? "", npc ?? "", "Empty npc id or role"));
                continue;
            }
            if (!roleToNpcs.TryGetValue(role, out var list))
            {
                list = new List<string>();
                roleToNpcs[role] = list;
            }
            list.Add(npc);
        }

        foreach (var (role, npcs) in roleToNpcs)
        {
            if (npcs.Count > 1)
                issues.Add(new CastingIssue("role_conflict", role, string.Join(",", npcs), $"Role '{role}' cast to {npcs.Count} NPCs"));
        }

        foreach (var role in requiredRoles)
        {
            if (!roleToNpcs.ContainsKey(role))
                issues.Add(new CastingIssue("missing_role", role, "", $"Required role '{role}' is not cast"));
        }

        if (npcFactions != null)
        {
            foreach (var (npc, role) in casting)
            {
                var expected = ExpectedFaction(role, config);
                if (expected is null || string.IsNullOrEmpty(expected)) continue;
                if (!npcFactions.TryGetValue(npc, out var actual)) continue; // unknown faction — skip
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new CastingIssue("faction_mismatch", role, npc,
                        $"NPC '{npc}' faction '{actual}' does not match expected '{expected}' for role '{role}'"));
            }
        }

        return issues;
    }

    public static bool IsValid(
        IReadOnlyDictionary<string, string> casting,
        CampaignConfig config,
        IReadOnlyDictionary<string, string>? npcFactions = null,
        IReadOnlyCollection<string>? requiredRoles = null)
        => Validate(casting, config, npcFactions, requiredRoles).Count == 0;

    /// <summary>
    /// Re-cast the required roles from a candidate pool (npcId -> faction), choosing an unused NPC
    /// whose faction matches the role's expected faction. Returns the new casting (npcId -> role)
    /// and whether every required role could be filled.
    /// </summary>
    public static (Dictionary<string, string> Casting, bool Complete) Repair(
        CampaignConfig config,
        IReadOnlyDictionary<string, string> candidatePool,
        GameRandom rng,
        IReadOnlyCollection<string>? requiredRoles = null)
    {
        requiredRoles ??= RequiredRoles;
        var casting = new Dictionary<string, string>();
        var used = new HashSet<string>();
        var complete = true;

        foreach (var role in requiredRoles)
        {
            var expected = ExpectedFaction(role, config);
            var candidates = candidatePool
                .Where(kv => !used.Contains(kv.Key)
                    && (string.IsNullOrEmpty(expected) || string.Equals(kv.Value, expected, StringComparison.OrdinalIgnoreCase)))
                .Select(kv => kv.Key)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            if (candidates.Count == 0)
            {
                complete = false;
                continue;
            }

            var pick = candidates[rng.Next(candidates.Count)];
            casting[pick] = role;
            used.Add(pick);
        }

        return (casting, complete);
    }
}
