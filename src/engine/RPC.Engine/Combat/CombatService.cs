using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Save;

namespace RPC.Engine.Combat;

public class CombatService
{
    private readonly EncounterTableRegistry? _encounterTables;
    private readonly ClassRegistry? _classRegistry;
    private readonly SynergyRegistry? _synergies;
    private readonly GameRandom _encounterRng;

    public CombatService(EncounterTableRegistry? encounterTables, ClassRegistry? classRegistry, GameRandom encounterRng, SynergyRegistry? synergies = null)
    {
        _encounterTables = encounterTables;
        _classRegistry = classRegistry;
        _encounterRng = encounterRng;
        _synergies = synergies;
    }

    public void TriggerEncounter(GameState state, EncounterDef? encounter = null)
    {
        state.StepsSinceEncounter = 0;

        if (encounter == null)
        {
            var tableId = state.CurrentDungeon?.WanderingTableId ?? state.CurrentDungeon?.EncounterTableId;
            if (tableId != null && _encounterTables != null)
            {
                encounter = _encounterTables.RollEncounter(tableId, _encounterRng);
            }
        }

        if (encounter == null)
        {
            encounter = new EncounterDef("random", "Random Encounter", new[]
            {
                new EnemySpawn("rat", _encounterRng.Roll(1, 2)),
                new EnemySpawn("goblin_scavenger", _encounterRng.Roll(0, 1))
            });
        }

        // Check for faction soldier parley, Ashmouth negotiation, or hostility
        var factionId = _encounterTables?.GetEncounterFaction(encounter.Id);
        if (factionId != null)
        {
            var rep = state.Reputation[factionId];
            var hasAshmouth = state.Party.Members.Any(m => m.ClassId == "ashmouth" && m.IsAlive);
            var options = new List<string>();
            if (rep >= 25) options.Add("Parley");
            // Bureau (bureaucracy) and Convocation (zealot order) accept formal diplomatic
            // protocols at high reputation: commission paperwork or doctrinal exchange.
            if (rep >= 25 && (factionId == "bureau" || factionId == "convocation"))
                options.Add("Diplomatic");
            if (hasAshmouth) options.Add("Negotiate");
            // Ancestral Bargaining (Compact signature mechanic): a Bonewarden can settle a
            // tithe-construct's claim with the Compact (inkblood) at standing >= 25 instead of fighting.
            if (factionId == "inkblood" && rep >= 25
                && state.Party.Members.Any(m => m.ClassId == "bonewarden" && m.IsAlive))
                options.Add("AncestralBargain");
            options.Add("Fight");

            if (options.Count > 1) // More than just "Fight"
            {
                state.CurrentParley = new ParleyOffer(encounter.Id, factionId, options.ToArray());
                state.CurrentEncounterId = Guid.NewGuid().ToString();
                state.Mode = GameMode.Exploration;
                state.EmitActionLog("combat", "encounter_parley_available", new Dictionary<string, string>
                {
                    { "encounterId", state.CurrentEncounterId },
                    { "factionId", factionId }
                });
                state.LastUpdate = DateTime.UtcNow;
                return;
            }
            else if (rep < -25)
            {
                // Hostile: reinforce the encounter
                var reinforced = encounter.Enemies.Concat(new[] { new EnemySpawn("faction_soldier", 1) }).ToArray();
                encounter = encounter with { Enemies = reinforced };
            }
        }

        EnterCombat(state, encounter);
    }

    private void EnterCombat(GameState state, EncounterDef encounter)
    {
        state.CurrentEncounterId = Guid.NewGuid().ToString();
        state.Combat = CombatEngine.Enter(state.Party, encounter, new GameRandom(_encounterRng.Roll(1, 10000)), environment: state.CurrentDungeonType);

        state.EmitActionLog("combat", "encounter_started", new Dictionary<string, string> { { "encounterId", state.CurrentEncounterId } });

        // Summon wild card faction ally if alliance is active
        if (state.IsWildCardAllianceActive && !string.IsNullOrEmpty(state.WildCardFactionId))
        {
            var allyDef = state.WildCardFactionId switch
            {
                "bureau" => new SummonDef("bureau_soldier", "Bureau Soldier", 20, 5, 4, 3),
                "convocation" => new SummonDef("convocation_zealot", "Convocation Zealot", 18, 6, 5, 3),
                "cartography" => new SummonDef("cartographer_scout", "Cartographer Scout", 15, 7, 3, 3),
                "stillness" => new SummonDef("stillness_agent", "Stillness Agent", 16, 6, 4, 3),
                "inkblood" => new SummonDef("inkblood_warden", "Inkblood Warden", 22, 4, 5, 3),
                _ => null
            };
            if (allyDef != null)
            {
                var rng = new GameRandom(_encounterRng.Roll(1, 10000));
                state.Combat = CombatEngine.SummonAlly(state.Combat, state.Party, allyDef, rng);
                state.EmitActionLog("combat", "wildcard_ally_summoned", new Dictionary<string, string>
                {
                    { "factionId", state.WildCardFactionId },
                    { "allyName", allyDef.Name }
                });
            }
        }

        if (state.Combat.IsFinished)
        {
            state.Mode = GameMode.Exploration;
            state.ClearTaggedEncounterTile(state.Combat.AllEnemiesDead);
            if (state.Combat.AllEnemiesDead && state.CurrentEncounterId != null)
            {
                state.EmitActionLog("combat", "encounter_won", new Dictionary<string, string> { { "encounterId", state.CurrentEncounterId } });
            }
            state.Combat = null;
        }
        else
        {
            state.Mode = GameMode.Combat;

            // Kick off the first round and auto-resolve any leading AI turns
            var rng = new GameRandom(_encounterRng.Roll(1, 10000));
            state.Combat = CombatEngine.AutoResolveToPlayerTurn(
                CombatEngine.Tick(state.Combat, null, rng, _classRegistry, null, _synergies),
                rng, _classRegistry, null, _synergies);
        }
        state.LastUpdate = DateTime.UtcNow;
    }

    public bool ResolveParley(GameState state, string choice)
    {
        if (state.CurrentParley == null) return false;

        var factionId = state.CurrentParley.FactionId;
        var normalized = choice?.ToLowerInvariant() ?? "fight";

        switch (normalized)
        {
            case "parley":
                ApplyReputationDelta(state, factionId, +2);
                state.EmitActionLog("combat", "encounter_parleyed", new Dictionary<string, string>
                {
                    { "encounterId", state.CurrentEncounterId ?? "unknown" },
                    { "factionId", factionId },
                    { "repDelta", "+2" }
                });
                ClosePeacefulEncounter(state);
                return true;

            case "diplomatic":
                return ResolveDiplomaticEncounter(state, factionId);

            case "negotiate":
                return ResolveAshmouthNegotiation(state);

            case "ancestralbargain":
                return ResolveAncestralBargain(state, factionId);

            case "escalate":
                ApplyReputationDelta(state, factionId, -5);
                state.EmitActionLog("combat", "encounter_escalated", new Dictionary<string, string>
                {
                    { "encounterId", state.CurrentEncounterId ?? "unknown" },
                    { "factionId", factionId },
                    { "repDelta", "-5" }
                });
                EscalateToReinforcedCombat(state, factionId);
                return true;

            case "fight":
            default:
                ApplyReputationDelta(state, factionId, -3);
                state.EmitActionLog("combat", "encounter_parley_refused", new Dictionary<string, string>
                {
                    { "encounterId", state.CurrentEncounterId ?? "unknown" },
                    { "factionId", factionId },
                    { "repDelta", "-3" }
                });
                var encounter = ResolveOrFallbackEncounter(state);
                state.CurrentParley = null;
                EnterCombat(state, encounter);
                return true;
        }
    }

    private bool ResolveDiplomaticEncounter(GameState state, string factionId)
    {
        // Bureau: commission paperwork yields intel; Convocation: doctrinal exchange yields blessing.
        // Both clear the encounter peacefully and grant a larger rep boost than plain Parley.
        string outcomeType;
        int repDelta = +5;
        switch (factionId)
        {
            case "bureau":
                outcomeType = "intel_exchanged";
                break;
            case "convocation":
                outcomeType = "blessing_granted";
                break;
            default:
                // Non-eligible factions fall through to standard parley behavior.
                outcomeType = "parley_accepted";
                repDelta = +2;
                break;
        }

        ApplyReputationDelta(state, factionId, repDelta);
        state.EmitActionLog("combat", "encounter_diplomatic", new Dictionary<string, string>
        {
            { "encounterId", state.CurrentEncounterId ?? "unknown" },
            { "factionId", factionId },
            { "outcome", outcomeType },
            { "repDelta", $"{(repDelta >= 0 ? "+" : "")}{repDelta}" }
        });
        ClosePeacefulEncounter(state);
        return true;
    }

    /// <summary>
    /// Compact signature resolution: a Bonewarden settles a tithe-construct's claim by paying the
    /// ancestral tithe — a fixed Compact (inkblood) reputation cost — and the encounter ends without
    /// combat. Requires a living Bonewarden and Compact standing >= 25; if a client sends the choice
    /// without meeting those conditions the engine degrades to a plain parley rather than granting it.
    /// </summary>
    private bool ResolveAncestralBargain(GameState state, string factionId)
    {
        var eligible = factionId == "inkblood"
            && state.Reputation[factionId] >= 25
            && state.Party.Members.Any(m => m.ClassId == "bonewarden" && m.IsAlive);

        if (!eligible)
        {
            // Safe degrade: behave like a plain parley.
            ApplyReputationDelta(state, factionId, +2);
            ClosePeacefulEncounter(state);
            return true;
        }

        const int titheCost = 5;
        ApplyReputationDelta(state, factionId, -titheCost);
        state.EmitActionLog("combat", "ancestral_bargain_struck", new Dictionary<string, string>
        {
            { "encounterId", state.CurrentEncounterId ?? "unknown" },
            { "factionId", factionId },
            { "repDelta", $"-{titheCost}" }
        });
        ClosePeacefulEncounter(state);
        return true;
    }

    private void EscalateToReinforcedCombat(GameState state, string factionId)
    {
        var encounter = ResolveOrFallbackEncounter(state);
        var reinforced = encounter.Enemies
            .Concat(new[] { new EnemySpawn("faction_soldier", 1) })
            .ToArray();
        encounter = encounter with { Enemies = reinforced };
        state.CurrentParley = null;
        EnterCombatWithSurprise(state, encounter);
    }

    private EncounterDef ResolveOrFallbackEncounter(GameState state)
    {
        var encounter = _encounterTables?.GetEncounterById(state.CurrentParley!.EncounterId);
        return encounter ?? new EncounterDef("random", "Random Encounter", new[]
        {
            new EnemySpawn("rat", _encounterRng.Roll(1, 2)),
            new EnemySpawn("goblin_scavenger", _encounterRng.Roll(0, 1))
        });
    }

    private static void ApplyReputationDelta(GameState state, string factionId, int delta)
    {
        var current = state.Reputation[factionId];
        state.Reputation[factionId] = Math.Clamp(current + delta, -100, 100);
    }

    /// <summary>
    /// Tear down parley state when the encounter ends without combat: clear parley + encounter id,
    /// mark the encounter tile resolved so the player can move on, and emit the wrap-up log entry.
    /// </summary>
    private static void ClosePeacefulEncounter(GameState state)
    {
        var encounterId = state.CurrentEncounterId;
        state.CurrentParley = null;
        state.Mode = GameMode.Exploration;
        state.ClearTaggedEncounterTile(resolved: true);
        if (encounterId != null)
        {
            state.EmitActionLog("combat", "encounter_resolved_peacefully",
                new Dictionary<string, string> { { "encounterId", encounterId } });
        }
        state.CurrentEncounterId = null;
    }

    private bool ResolveAshmouthNegotiation(GameState state)
    {
        if (state.CurrentParley == null) return false;

        var factionId = state.CurrentParley.FactionId;

        var ashmouth = state.Party.Members
            .Where(m => m.ClassId == "ashmouth" && m.IsAlive)
            .OrderByDescending(m => m.Level)
            .FirstOrDefault();

        if (ashmouth.Id == Guid.Empty)
        {
            state.CurrentParley = null;
            return false;
        }

        // Enemy leader level approximated by encounter danger
        var leaderLevel = 2;
        var repModifier = state.Reputation[factionId] / 10;
        // Ashmouth Broker branch bonus (per spec doc 05): broker training adds +2 to the
        // total. We approximate "trained" as level >= 3 since branches are picked at L3.
        var brokerBonus = ashmouth.Level >= 3 ? 2 : 0;
        var successThreshold = leaderLevel - repModifier;
        var roll = _encounterRng.Roll(1, 6);
        var total = ashmouth.Level + roll + brokerBonus;

        var logMeta = new Dictionary<string, string>
        {
            { "encounterId", state.CurrentEncounterId ?? "unknown" },
            { "factionId", factionId },
            { "ashmouthLevel", ashmouth.Level.ToString() },
            { "roll", roll.ToString() },
            { "brokerBonus", brokerBonus.ToString() }
        };

        if (total >= successThreshold + 3)
        {
            // Complete success: rep boost + peaceful resolution
            ApplyReputationDelta(state, factionId, +3);
            logMeta["repDelta"] = "+3";
            state.EmitActionLog("combat", "negotiation_complete_success", logMeta);
            ClosePeacefulEncounter(state);
            return true;
        }
        else if (total >= successThreshold)
        {
            // Partial success: small rep boost, peaceful resolution
            ApplyReputationDelta(state, factionId, +1);
            logMeta["repDelta"] = "+1";
            state.EmitActionLog("combat", "negotiation_partial_success", logMeta);
            ClosePeacefulEncounter(state);
            return true;
        }
        else
        {
            // Failure: faction insulted, surprise combat, larger rep penalty
            ApplyReputationDelta(state, factionId, -4);
            logMeta["repDelta"] = "-4";
            state.EmitActionLog("combat", "negotiation_failure", logMeta);

            var encounter = ResolveOrFallbackEncounter(state);
            state.CurrentParley = null;
            EnterCombatWithSurprise(state, encounter);
            return true;
        }
    }

    private void EnterCombatWithSurprise(GameState state, EncounterDef encounter)
    {
        EnterCombat(state, encounter);

        if (state.Combat != null && !state.Combat.IsFinished)
        {
            var enemies = state.Combat.Combatants.Where(c => !c.IsPlayer && c.IsAlive).ToArray();
            var players = state.Combat.Combatants.Where(c => c.IsPlayer && c.IsAlive).ToArray();

            var newCombatants = state.Combat.Combatants.ToArray();
            var newLog = new List<CombatLogEntry>(state.Combat.Log);

            foreach (var enemy in enemies)
            {
                if (players.Length == 0) break;
                var target = players[_encounterRng.Roll(0, players.Length - 1)];
                var targetIdx = Array.FindIndex(newCombatants, c => c.Id == target.Id);
                if (targetIdx < 0) continue;

                var damage = _encounterRng.Roll(1, 4) + 1;
                var newHp = Math.Max(0, target.Hp - damage);
                newCombatants[targetIdx] = newCombatants[targetIdx] with { Hp = newHp };
                newLog.Add(new(enemy.Id, $"{enemy.Name} surprises {target.Name} for {damage} damage!", state.Combat.Round));
            }

            state.Combat = state.Combat with { Combatants = newCombatants, Log = newLog };
        }
    }

    public bool SubmitCombatAction(GameState state, CombatAction action)
    {
        if (state.Combat == null || state.Mode != GameMode.Combat) return false;

        // Validate ability row requirements
        if (action.Type == ActionType.UseAbility && action.AbilityId is not null)
        {
            var actor = state.Combat.Combatants.FirstOrDefault(c => c.Id == action.ActorId);
            if (actor.Id != Guid.Empty)
            {
                var member = state.Party.Members.FirstOrDefault(m => m.Id == action.ActorId);
                if (member.Id != Guid.Empty && _classRegistry?.Get(member.ClassId) is { } classDef)
                {
                    var ability = classDef.Abilities.FirstOrDefault(a => a.Id == action.AbilityId);
                    if (ability is not null && !ability.IsAvailableInRow(actor.Row))
                        return false;
                }
            }
        }

        var rng = new GameRandom(_encounterRng.Roll(1, 10000));
        Action<string, string, Dictionary<string, string>> emitter = (cat, type, payload) =>
        {
            if (type == "synergy_triggered" && state.CurrentEncounterId != null)
            {
                payload["encounterId"] = state.CurrentEncounterId;
            }
            if (type == "synergy_triggered" && payload.TryGetValue("synergyId", out var sid) && !string.IsNullOrEmpty(sid))
            {
                state.Journal.Discover(sid);
                state.Analytics.RecordSynergyDiscovered(sid);
            }
            state.EmitActionLog(cat, type, payload);
        };

        state.Combat = CombatEngine.Tick(state.Combat, action, rng, _classRegistry, emitter, _synergies);

        // Combat→dungeon bridge: an area/AoE damage ability resolving in the encounter reveals any
        // breakable wall adjacent to the party's dungeon tile (the AoE origin), matching the
        // explicit-search reveal. Combat is range-band based, so the encounter's tile is used.
        if (IsAreaDamageAbility(state, action))
            Exploration.ExplorationService.RevealBreakableWallsFromAreaDamage(state);

        // Auto-resolve AI turns
        state.Combat = CombatEngine.AutoResolveToPlayerTurn(state.Combat, rng, _classRegistry, emitter, _synergies);

        if (state.Combat.IsFinished)
        {
            var allEnemiesDead = state.Combat.AllEnemiesDead;
            var allPlayersDead = state.Combat.AllPlayersDead;

            // Apply combat results to party.
            // Bench characters are not combatants, so they intrinsically gain no XP here.
            // Field promotion: active members whose level is below the active-party average gain
            // +50% bonus XP so under-levelled members catch up to the rest of the active party.
            var levelUps = new List<string>();
            var scaledXpReward = state.Combat.XpReward * state.CurrentAct;
            var activeLevels = state.Party.Members.Where(m => m.Id != Guid.Empty).Select(m => m.Level).ToList();
            double averageActiveLevel = activeLevels.Count > 0 ? activeLevels.Average() : 0;
            int XpFor(CharacterState m) =>
                m.Level < averageActiveLevel ? (int)Math.Round(scaledXpReward * 1.5) : scaledXpReward;
            foreach (var combatant in state.Combat.Combatants.Where(c => c.IsPlayer))
            {
                var member = state.Party.Members.FirstOrDefault(m => m.Id == combatant.Id);
                if (member.Id != Guid.Empty)
                {
                    var index = Array.IndexOf(state.Party.Members, member);

                    if (combatant.Hp <= 0)
                    {
                        bool stabilized = combatant.StatusEffects.Any(s => s.Type == "stabilized");
                        if (stabilized)
                        {
                            var saved = member with { CurrentHp = 1, Xp = member.Xp + XpFor(member), TempModifiers = combatant.TempModifiers };
                            state.Party.SetMember(index, saved);
                            state.EmitActionLog("combat", "character_stabilized", new Dictionary<string, string>
                            {
                                { "characterId", member.Id.ToString() },
                                { "characterName", member.Name }
                            });
                        }
                        else
                        {
                            state.Party.DeadCharacters.Add(member with { CurrentHp = 0, TempModifiers = Array.Empty<TempStatModifier>() });
                            state.Party.SetMember(index, default);
                            state.EmitActionLog("combat", "character_died", new Dictionary<string, string>
                            {
                                { "characterId", member.Id.ToString() },
                                { "characterName", member.Name }
                            });
                        }
                        continue;
                    }

                    var newXp = member.Xp + XpFor(member);
                    var updated = member with { CurrentHp = combatant.Hp, Xp = newXp, TempModifiers = combatant.TempModifiers };

                    // Check for level ups
                    if (_classRegistry?.Get(member.ClassId) is { } classDef)
                    {
                        var beforeLevel = updated.Level;
                        updated = LevelingSystem.CheckAndApplyLevelUps(updated, classDef);
                        if (updated.Level > beforeLevel)
                        {
                            levelUps.Add(updated.Name);
                        }
                    }

                    state.Party.SetMember(index, updated);
                }
            }

            state.LastCombatResult = new CombatResult(
                allEnemiesDead,
                state.Combat.XpReward * state.CurrentAct,
                levelUps.ToArray(),
                state.Combat.Round);

            state.Mode = GameMode.Exploration;
            state.Combat = null;

            state.ClearTaggedEncounterTile(allEnemiesDead);

            if (allEnemiesDead && state.CurrentEncounterId != null)
            {
                state.EmitActionLog("combat", "encounter_won", new Dictionary<string, string> { { "encounterId", state.CurrentEncounterId } });
            }

            state.ResolveTravelCombatOutcome(allEnemiesDead ? "victory" : "defeat");

            // Rescue expedition failure: rescue party wiped out
            if (allPlayersDead && state.RescueExpedition?.IsActive == true)
            {
                state.ResolveRescueExpedition(success: false);
                state.Mode = GameMode.Menu;
                state.CurrentDungeon = null;
                state.CurrentDungeonType = null;
                state.Exploration.Reset();
                return true;
            }

            // Ironman: attempt rescue expedition on total party kill
            if (allPlayersDead && state.IsIronman)
            {
                var rescueStarted = state.StartRescueExpedition();
                if (!rescueStarted)
                {
                    // No bench characters available: the run is over.
                    state.EndIronmanRun(rescueFailed: false);
                }
            }
        }

        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// True when the submitted action is a player ability that deals area damage — a damage-type
    /// effect carrying the content-driven <c>area</c> tag. These are the abilities whose blast
    /// reaches the dungeon walls around the encounter, so they can shatter an adjacent breakable wall.
    /// </summary>
    private bool IsAreaDamageAbility(GameState state, CombatAction action)
    {
        if (action.Type != ActionType.UseAbility || action.AbilityId is null) return false;
        var member = state.Party.Members.FirstOrDefault(m => m.Id == action.ActorId);
        if (member.Id == Guid.Empty || _classRegistry?.Get(member.ClassId) is not { } classDef) return false;
        var ability = classDef.Abilities.FirstOrDefault(a => a.Id == action.AbilityId);
        return ability is not null
            && ability.Effect.Type == "damage"
            && ability.Tags.Contains("area");
    }

    public void FleeCombat(GameState state)
    {
        if (state.Mode != GameMode.Combat) return;
        state.Mode = GameMode.Exploration;
        if (state.CurrentEncounterId != null)
        {
            state.EmitActionLog("combat", "encounter_fled", new Dictionary<string, string> { { "encounterId", state.CurrentEncounterId } });
        }
        state.ResolveTravelCombatOutcome("flee");
        state.Combat = null;
        state.ClearTaggedEncounterTile(resolved: false);
        state.LastUpdate = DateTime.UtcNow;
    }
}
