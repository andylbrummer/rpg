using RPC.Engine.Content;

namespace RPC.Engine.Combat;

/// <summary>
/// Resolves a consumable item's <see cref="ItemEffect"/> against a target combatant in combat.
/// Pure: returns the modified target plus a log message; never mutates inputs. Fail-fast on a
/// consumable that carries no effect or an unrecognised effect type (no fabricated effect).
/// </summary>
public static class ConsumableSystem
{
    public static (Combatant Target, string LogMessage) ApplyEffect(
        ItemDef item, Combatant actor, Combatant target, GameRandom rng)
    {
        var effect = item.Effect
            ?? throw new InvalidOperationException($"Consumable {item.Id} has no effect to apply.");

        switch (effect.Type)
        {
            case "heal":
            {
                var amount = RollAmount(effect.Value, actor, rng);
                var newHp = Math.Min(target.MaxHp, target.Hp + amount);
                var healed = newHp - target.Hp;
                return (target with { Hp = newHp },
                    $"{actor.Name} uses {item.Name} on {target.Name}, healing {healed} HP");
            }

            case "damage":
            {
                var amount = RollAmount(effect.Value, actor, rng);
                var newHp = Math.Max(0, target.Hp - amount);
                return (target with { Hp = newHp },
                    $"{actor.Name} uses {item.Name} on {target.Name} for {amount} damage");
            }

            case "buff":
            {
                var (statusType, duration, potency) = ParseStatus(effect.Value);
                var effects = new List<StatusEffect>(target.StatusEffects)
                {
                    new(statusType, duration, potency, actor.Id)
                };
                return (target with { StatusEffects = effects },
                    $"{actor.Name} uses {item.Name} on {target.Name}, granting {statusType}");
            }

            case "cure_status":
            {
                var statusType = effect.Value.Split(':')[0];
                var effects = target.StatusEffects.Where(s => s.Type != statusType).ToList();
                return (target with { StatusEffects = effects },
                    $"{actor.Name} uses {item.Name} on {target.Name}, curing {statusType}");
            }

            default:
                throw new InvalidOperationException(
                    $"Consumable {item.Id} has an unsupported effect type: {effect.Type}");
        }
    }

    /// <summary>
    /// Parses an effect value as either a flat integer ("10") or dice notation ("2d6+4",
    /// "1d8+PWR"). Mirrors the ability-damage grammar so consumable and ability authoring agree.
    /// </summary>
    private static int RollAmount(string value, Combatant actor, GameRandom rng)
    {
        if (int.TryParse(value, out var flat))
            return Math.Max(0, flat);

        var parts = value.Split('+');
        var diceParts = parts[0].Split('d');
        if (diceParts.Length != 2 || !int.TryParse(diceParts[0], out var count) || !int.TryParse(diceParts[1], out var sides))
            throw new InvalidOperationException($"Invalid consumable effect value: {value}");

        var bonus = 0;
        if (parts.Length > 1)
        {
            if (parts[1] == "PWR")
                bonus = actor.Power;
            else
                int.TryParse(parts[1], out bonus);
        }

        var roll = 0;
        for (int i = 0; i < count; i++)
            roll += rng.Roll(1, sides);

        return Math.Max(0, roll + bonus);
    }

    /// <summary>
    /// Parses a buff value: "type", "type:duration", or "type:duration:potency".
    /// Duration defaults to 3 rounds; potency defaults to null.
    /// </summary>
    private static (string Type, int Duration, int? Potency) ParseStatus(string value)
    {
        var parts = value.Split(':');
        var type = parts[0];
        if (string.IsNullOrEmpty(type))
            throw new InvalidOperationException($"Invalid buff effect value: {value}");
        var duration = parts.Length > 1 && int.TryParse(parts[1], out var d) ? d : 3;
        int? potency = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : null;
        return (type, duration, potency);
    }
}
