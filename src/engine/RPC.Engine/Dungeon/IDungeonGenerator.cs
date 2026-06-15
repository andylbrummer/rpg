using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

/// <summary>
/// Contract for the dungeon generation module. Implementations must be deterministic:
/// for a given <paramref name="dungeonType"/>, a given <paramref name="seed"/>, and a
/// fixed set of constructor inputs (segments, templates, encounter tables), every call
/// to <see cref="Generate"/> must produce a structurally-equal <see cref="Dungeon"/>.
///
/// Determinism rules implementations must honour:
/// <list type="bullet">
///   <item>All randomness routes through a seeded <see cref="Combat.GameRandom"/> derived
///         from the caller-supplied seed (or a stable hash of <paramref name="dungeonType"/>
///         when no seed is supplied).</item>
///   <item>No use of <c>Random.Shared</c>, ambient <see cref="System.Random"/>,
///         <see cref="System.DateTime"/> clocks, <see cref="System.Guid.NewGuid"/>, or any
///         other source of non-reproducible entropy.</item>
///   <item>The output is a fully-connected <see cref="Dungeon"/>; disconnected layouts
///         must either be rebuilt or surface as an exception, never returned.</item>
/// </list>
/// </summary>
public interface IDungeonGenerator
{
    /// <summary>
    /// Generate a dungeon from an explicit <see cref="DungeonGenerationRequest"/> and return it
    /// alongside the <see cref="DungeonGenerationIdentity"/> that reproduces it. This is the
    /// primary generation contract: persisting the returned identity and replaying it through
    /// this method yields a structurally-equal dungeon (see interface remarks).
    /// </summary>
    DungeonGenerationResult Generate(DungeonGenerationRequest request);

    /// <summary>
    /// Convenience overload for callers that only need the dungeon. Delegates to
    /// <see cref="Generate(DungeonGenerationRequest)"/>; same <c>(dungeonType, seed)</c> pair
    /// always returns a structurally-equal dungeon.
    /// </summary>
    /// <param name="dungeonType">Identifier matching a registered template id, or any
    /// string when no template is registered (procedural fallback).</param>
    /// <param name="seed">Explicit seed for the deterministic RNG. When <c>null</c>, a
    /// stable hash of <paramref name="dungeonType"/> is used so the call remains
    /// reproducible.</param>
    Dungeon Generate(string dungeonType, int? seed = null);
}
