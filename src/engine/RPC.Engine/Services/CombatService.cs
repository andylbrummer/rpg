using RPC.Engine.Character;
using RPC.Engine.Combat;

namespace RPC.Engine.Services;

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
        state._stepsSinceEncounter = 0;

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
            if (hasAshmouth) options.Add("Negotiate");
            options.Add("Fight");

            if (options.Count > 1) // More than just "Fight"
            {
                state.CurrentParley = new ParleyOffer(encounter.Id, factionId, options.ToArray());
                state._currentEncounterId = Guid.NewGuid().ToString();
                state.Mode = GameMode.Exploration;
                state.EmitActionLog("combat", "encounter_parley_available", new Dictionary<string, string>
                {
                    { "encounterId", state._currentEncounterId },
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
        state._currentEncounterId = Guid.NewGuid().ToString();
        state.Combat = CombatEngine.Enter(state.Party, encounter, new GameRandom(_encounterRng.Roll(1, 10000)));

        state.EmitActionLog("combat", "encounter_started", new Dictionary<string, string> { { "encounterId", state._currentEncounterId } });

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
            if (state.Combat.AllEnemiesDead && state._currentEncounterId != null)
            {
                state.EmitActionLog("combat", "encounter_won", new Dictionary<string, string> { { "encounterId", state._currentEncounterId } });
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

        if (choice == "parley")
        {
            state.EmitActionLog("combat", "encounter_parleyed", new Dictionary<string, string>
            {
                { "encounterId", state._currentEncounterId ?? "unknown" },
                { "factionId", state.CurrentParley.FactionId }
            });
            state.CurrentParley = null;
            state.Mode = GameMode.Exploration;
            return true;
        }
        else if (choice == "negotiate")
        {
            return ResolveAshmouthNegotiation(state);
        }
        else
        {
            var encounter = _encounterTables?.GetEncounterById(state.CurrentParley.EncounterId);
            if (encounter == null)
            {
                encounter = new EncounterDef("random", "Random Encounter", new[]
                {
                    new EnemySpawn("rat", _encounterRng.Roll(1, 2)),
                    new EnemySpawn("goblin_scavenger", _encounterRng.Roll(0, 1))
                });
            }
            state.CurrentParley = null;
            EnterCombat(state, encounter);
            return true;
        }
    }

    private bool ResolveAshmouthNegotiation(GameState state)
    {
        if (state.CurrentParley == null) return false;

        var factionId = state.CurrentParley.FactionId;
        var encounter = _encounterTables?.GetEncounterById(state.CurrentParley.EncounterId);

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
        var successThreshold = leaderLevel - repModifier;
        var roll = _encounterRng.Roll(1, 6);
        var total = ashmouth.Level + roll;

        if (total >= successThreshold + 3)
        {
            // Complete success
            state.EmitActionLog("combat", "negotiation_complete_success", new Dictionary<string, string>
            {
                { "encounterId", state._currentEncounterId ?? "unknown" },
                { "factionId", factionId },
                { "ashmouthLevel", ashmouth.Level.ToString() },
                { "roll", roll.ToString() }
            });
            state.CurrentParley = null;
            state.Mode = GameMode.Exploration;
            return true;
        }
        else if (total >= successThreshold)
        {
            // Partial success
            state.EmitActionLog("combat", "negotiation_partial_success", new Dictionary<string, string>
            {
                { "encounterId", state._currentEncounterId ?? "unknown" },
                { "factionId", factionId },
                { "ashmouthLevel", ashmouth.Level.ToString() },
                { "roll", roll.ToString() }
            });
            state.CurrentParley = null;
            state.Mode = GameMode.Exploration;
            return true;
        }
        else
        {
            // Failure - combat with surprise round
            state.EmitActionLog("combat", "negotiation_failure", new Dictionary<string, string>
            {
                { "encounterId", state._currentEncounterId ?? "unknown" },
                { "factionId", factionId },
                { "ashmouthLevel", ashmouth.Level.ToString() },
                { "roll", roll.ToString() }
            });

            if (encounter == null)
            {
                encounter = new EncounterDef("random", "Random Encounter", new[]
                {
                    new EnemySpawn("rat", _encounterRng.Roll(1, 2)),
                    new EnemySpawn("goblin_scavenger", _encounterRng.Roll(0, 1))
                });
            }
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
            if (type == "synergy_triggered" && state._currentEncounterId != null)
            {
                payload["encounterId"] = state._currentEncounterId;
            }
            if (type == "synergy_triggered" && payload.TryGetValue("synergyId", out var sid) && !string.IsNullOrEmpty(sid))
            {
                state.Journal.Discover(sid);
            }
            state.EmitActionLog(cat, type, payload);
        };

        state.Combat = CombatEngine.Tick(state.Combat, action, rng, _classRegistry, emitter, _synergies);

        // Auto-resolve AI turns
        state.Combat = CombatEngine.AutoResolveToPlayerTurn(state.Combat, rng, _classRegistry, emitter, _synergies);

        if (state.Combat.IsFinished)
        {
            var allEnemiesDead = state.Combat.AllEnemiesDead;

            // Apply combat results to party
            var levelUps = new List<string>();
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
                            var saved = member with { CurrentHp = 1, Xp = member.Xp + state.Combat.XpReward * state.CurrentAct, TempModifiers = combatant.TempModifiers };
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

                    var scaledXpReward = state.Combat.XpReward * state.CurrentAct;
                    var newXp = member.Xp + scaledXpReward;
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

            if (allEnemiesDead && state._currentEncounterId != null)
            {
                state.EmitActionLog("combat", "encounter_won", new Dictionary<string, string> { { "encounterId", state._currentEncounterId } });
            }
        }

        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public void FleeCombat(GameState state)
    {
        if (state.Mode != GameMode.Combat) return;
        state.Mode = GameMode.Exploration;
        state.Combat = null;
        state.ClearTaggedEncounterTile(resolved: false);
        state.LastUpdate = DateTime.UtcNow;
    }
}
