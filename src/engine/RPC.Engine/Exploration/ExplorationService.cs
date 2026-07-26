using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Dungeons;

namespace RPC.Engine.Exploration;

public class ExplorationService
{
    private readonly EncounterTableRegistry? _encounterTables;
    private readonly ClassRegistry? _classRegistry;
    private readonly GameRandom _encounterRng;

    public ExplorationService(EncounterTableRegistry? encounterTables, ClassRegistry? classRegistry, GameRandom encounterRng)
    {
        _encounterTables = encounterTables;
        _classRegistry = classRegistry;
        _encounterRng = encounterRng;
    }

    public void EnterDungeon(GameState state, Dungeon dungeon, string dungeonType)
    {
        if (state.CampaignEnded) return;
        if (state.HasPendingBranchChoices) return;
        if (state.Heat.IsLockdown)
        {
            state.EmitActionLog("heat", "dungeon_blocked_lockdown", new Dictionary<string, string>
            {
                { "heat", state.Heat.Value.ToString() }
            });
            return;
        }
        state.CurrentDungeon = dungeon;
        state.CurrentDungeonType = dungeonType;
        state.ExploredTiles.Clear();
        state.Exploration.CollectedLoot.Clear();
        state.StepsSinceEncounter = 0;
        state.PendingTaggedEncounterTile = null;
        state.Mode = GameMode.Exploration;
        state.EmitActionLog("dungeon", "dungeon_entered", new Dictionary<string, string> { { "dungeonType", dungeonType } });
        state.IncrementTurns(1);
        if (dungeon.FindEntrance() is not { } entrance) return;
        state.Player.Position = entrance;
        state.Player.Facing = Direction.North;
        ExploreAroundPlayer(state);
    }

    public void ExploreAroundPlayer(GameState state)
    {
        if (state.CurrentDungeon == null) return;
        var px = state.Player.Position.X;
        var py = state.Player.Position.Y;
        var viewRadius = 3;
        int newTiles = 0;

        for (int x = Math.Max(0, px - viewRadius); x < Math.Min(state.CurrentDungeon.Width, px + viewRadius + 1); x++)
        {
            for (int y = Math.Max(0, py - viewRadius); y < Math.Min(state.CurrentDungeon.Height, py + viewRadius + 1); y++)
            {
                var tile = state.CurrentDungeon.Tiles[x, y];
                if (tile.Type != TileType.Empty)
                {
                    if (state.ExploredTiles.Add($"{x},{y}"))
                        newTiles++;
                }
            }
        }

        if (newTiles > 0)
        {
            const int exploreXpPerTile = 5;
            var totalExploreXp = newTiles * exploreXpPerTile;
            for (int i = 0; i < state.Party.Members.Length; i++)
            {
                var member = state.Party.Members[i];
                if (member.Id == Guid.Empty) continue;
                var updated = member with { Xp = member.Xp + totalExploreXp };
                if (_classRegistry?.Get(member.ClassId) is { } classDef)
                {
                    updated = LevelingSystem.CheckAndApplyLevelUps(updated, classDef);
                }
                state.Party.SetMember(i, updated);
            }
        }
    }

    public bool TryMoveForward(GameState state) => ExecuteMove(state, state.Player.Facing);
    public bool TryMoveBack(GameState state) => ExecuteMove(state, state.Player.Facing.Opposite());
    public bool TryStrafeLeft(GameState state) => ExecuteMove(state, state.Player.Facing.StrafeLeft());
    public bool TryStrafeRight(GameState state) => ExecuteMove(state, state.Player.Facing.StrafeRight());

    private bool ExecuteMove(GameState state, Direction dir)
    {
        if (state.CurrentDungeon == null) return false;
        if (state.Mode == GameMode.Combat) return false;

        var newPos = state.Player.Position.Move(dir);
        if (state.CurrentDungeon.CanMoveTo(state.Player.Position, dir))
        {
            state.Player.Position = newPos;
            ExploreAroundPlayer(state);
            state.LastUpdate = DateTime.UtcNow;
            state.StepsSinceEncounter++;

            // A step inside a dungeon node is one in-dungeon turn: age carried bloom samples.
            Inventory.BloomDecaySystem.TickDungeonTurn(state);

            // End-of-movement: the Inkblood Cartographer senses nearby secrets onto the automap.
            AutoDetectSecrets(state);

            var tile = state.CurrentDungeon.GetTile(newPos);
            if (!string.IsNullOrEmpty(tile.EncounterId))
            {
                var encounter = _encounterTables?.GetEncounterById(tile.EncounterId);
                if (encounter != null)
                {
                    state.PendingTaggedEncounterTile = newPos;
                    state.TriggerEncounter(encounter);
                    return true;
                }
            }

            // Check rescue expedition arrival
            if (state.RescueExpedition?.IsActive == true && state.Player.Position == state.RescueExpedition.TpkLocation)
            {
                state.ResolveRescueExpedition(success: true);
                return true;
            }

            var encounterChance = 0.05 + (state.StepsSinceEncounter * 0.08);
            if (_encounterRng.Roll(0, 99) < encounterChance * 100)
            {
                state.TriggerEncounter();
            }

            return true;
        }
        return false;
    }

    // ---- Breakable walls + Cartographer detection (T51b) ----

    private const string CartographerClassId = "inkblood";

    /// <summary>True when the party fields an Inkblood — whose Cartographer discipline grants the
    /// passive 2-tile secret-sensing. The Cartographer is an Inkblood role, not a separate class.</summary>
    private bool HasCartographer(GameState state)
    {
        foreach (var member in state.Party.Members)
        {
            if (member.Id == Guid.Empty) continue;
            if (string.Equals(member.ClassId, CartographerClassId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Inkblood Cartographer passive: at the end of each movement, sense every positioned secret
    /// within <c>Chebyshev &lt;= 2</c> of the party and mark it detected (automap "?"). Detection
    /// reveals existence, not type — explicit search promotes it to a full discovery.
    /// </summary>
    public void AutoDetectSecrets(GameState state)
    {
        if (state.CurrentDungeon == null) return;
        if (!HasCartographer(state)) return;

        var party = state.Player.Position;
        foreach (var secret in state.Secrets.All)
        {
            if (secret.X is not int sx || secret.Y is not int sy) continue;
            if (state.Journal.IsDiscovered(secret.Id) || state.Journal.IsDetected(secret.Id)) continue;
            if (party.ChebyshevDistance(new Position(sx, sy)) <= 2)
                state.DetectSecret(secret.Id, "cartographer");
        }
    }

    /// <summary>
    /// Explicit search of the party's immediate surroundings (<c>Chebyshev &lt;= 1</c>): fully
    /// reveals the type of any positioned secret there. A revealed breakable wall switches from
    /// hidden <c>BreakableWall</c> material to visible <c>CrackedWall</c>. Returns the secret ids
    /// newly discovered by this search.
    /// </summary>
    public IReadOnlyList<string> SearchForSecrets(GameState state)
    {
        if (state.CurrentDungeon == null) return Array.Empty<string>();

        var party = state.Player.Position;
        var found = new List<string>();
        foreach (var secret in state.Secrets.All)
        {
            if (secret.X is not int sx || secret.Y is not int sy) continue;
            if (state.Journal.IsDiscovered(secret.Id)) continue;
            if (party.ChebyshevDistance(new Position(sx, sy)) > 1) continue;

            if (state.DiscoverSecret(secret.Type, secret.Id, "search"))
            {
                found.Add(secret.Id);
                RevealCrackedWall(state, secret);
            }
        }
        if (found.Count > 0)
            state.LastUpdate = DateTime.UtcNow;
        return found;
    }

    /// <summary>
    /// Break a discovered breakable wall: open its border on both adjacent tiles and spend one
    /// in-dungeon turn (aging carried bloom samples via the shared dungeon-turn tick). The secret
    /// must be a fully-discovered <c>breakable_wall</c> carrying a tile position. Returns true when
    /// the wall was opened.
    /// </summary>
    public bool BreakWall(GameState state, string secretId)
    {
        if (state.CurrentDungeon == null) return false;
        var secret = state.Secrets.Get(secretId);
        if (secret is null || secret.Type != "breakable_wall") return false;
        if (!state.Journal.IsDiscovered(secretId)) return false; // must know its type before breaking
        if (secret.X is not int sx || secret.Y is not int sy) return false;
        if (!TryParseWall(secret.Wall, out var dir)) return false;

        SetBorderBothSides(state.CurrentDungeon, new Position(sx, sy), dir, BorderType.None);

        // One break = one in-dungeon turn (ages carried bloom samples).
        Inventory.BloomDecaySystem.TickDungeonTurn(state);

        state.EmitActionLog("dungeon", "wall_broken", new Dictionary<string, string>
        {
            { "secretId", secretId },
            { "x", sx.ToString() },
            { "y", sy.ToString() },
            { "wall", dir.ToString() }
        });
        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Combat→dungeon bridge: an area/AoE damage ability resolving in combat reveals any breakable
    /// wall adjacent (<c>Chebyshev &lt;= 1</c>) to the party's current dungeon tile, fully discovering
    /// it with trigger <c>area_damage</c> and flipping its border <c>BreakableWall -&gt; CrackedWall</c>
    /// on both sides — the same end-state as an explicit <see cref="SearchForSecrets"/>. Combat is
    /// range-band based and combatants carry no tile coordinates, so the encounter's dungeon tile
    /// (the party position) is used as the AoE origin. Returns the secret ids newly discovered.
    /// No-op outside a dungeon. Reuses the <see cref="RevealCrackedWall"/> border-flip helper shared
    /// with the explicit-search path.
    /// </summary>
    public static IReadOnlyList<string> RevealBreakableWallsFromAreaDamage(GameState state)
    {
        if (state.CurrentDungeon == null) return Array.Empty<string>();

        var party = state.Player.Position;
        var found = new List<string>();
        foreach (var secret in state.Secrets.All)
        {
            if (secret.Type != "breakable_wall") continue;
            if (secret.X is not int sx || secret.Y is not int sy) continue;
            if (state.Journal.IsDiscovered(secret.Id)) continue;
            if (party.ChebyshevDistance(new Position(sx, sy)) > 1) continue;

            if (state.DiscoverSecret(secret.Type, secret.Id, "area_damage"))
            {
                found.Add(secret.Id);
                RevealCrackedWall(state, secret);
            }
        }
        if (found.Count > 0)
            state.LastUpdate = DateTime.UtcNow;
        return found;
    }

    private static void RevealCrackedWall(GameState state, Dungeons.SecretDef secret)
    {
        if (secret.Type != "breakable_wall") return;
        if (state.CurrentDungeon == null) return;
        if (secret.X is not int sx || secret.Y is not int sy) return;
        if (!TryParseWall(secret.Wall, out var dir)) return;

        var pos = new Position(sx, sy);
        if (state.CurrentDungeon.GetTile(pos).GetBorder(dir) == BorderType.BreakableWall)
            SetBorderBothSides(state.CurrentDungeon, pos, dir, BorderType.CrackedWall);
    }

    private static void SetBorderBothSides(Dungeon dungeon, Position pos, Direction dir, BorderType border)
    {
        if (dungeon.IsValidPosition(pos))
            dungeon.Tiles[pos.X, pos.Y] = dungeon.Tiles[pos.X, pos.Y].WithBorder(dir, border);

        var neighbour = pos.Move(dir);
        if (dungeon.IsValidPosition(neighbour))
            dungeon.Tiles[neighbour.X, neighbour.Y] = dungeon.Tiles[neighbour.X, neighbour.Y].WithBorder(dir.Opposite(), border);
    }

    private static bool TryParseWall(string? wall, out Direction dir)
    {
        dir = Direction.North;
        return !string.IsNullOrEmpty(wall) && Enum.TryParse(wall, ignoreCase: true, out dir);
    }

    public void TurnLeft(GameState state)
    {
        state.Player.TurnLeft();
        state.LastUpdate = DateTime.UtcNow;
    }

    public void TurnRight(GameState state)
    {
        state.Player.TurnRight();
        state.LastUpdate = DateTime.UtcNow;
    }
}
