using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

/// <summary>
/// Input contract for the dungeon generation module. Together with the generator's fixed
/// constructor inputs (segments, templates, encounter/loot tables) this fully determines the
/// produced dungeon, so a persisted <see cref="DungeonGenerationIdentity"/> can reproduce it.
/// </summary>
/// <param name="DungeonType">Template id, or any string when no template is registered
/// (procedural fallback).</param>
/// <param name="Seed">Explicit seed for the deterministic RNG. When <c>null</c>, the generator
/// derives a stable seed from <paramref name="DungeonType"/> so the call stays reproducible.</param>
/// <param name="ContentHash">Identity of the content pack the request is resolved against.
/// Carried through to the result identity for save/load validation; it does not alter layout.</param>
public record DungeonGenerationRequest(string DungeonType, int? Seed = null, string? ContentHash = null);

/// <summary>
/// The resolved identity that fully determines regeneration of a dungeon: its type, the
/// effective seed actually used (never null, even when the request omitted one), and the content
/// hash it was built against. Persisting this is sufficient to reproduce a structurally-equal
/// dungeon via <see cref="IDungeonGenerator.Generate(DungeonGenerationRequest)"/>.
/// </summary>
public record DungeonGenerationIdentity(string DungeonType, int Seed, string? ContentHash);

/// <summary>
/// Output contract for the dungeon generation module: the generated <see cref="Dungeon"/> plus the
/// <see cref="DungeonGenerationIdentity"/> that reproduces it.
/// </summary>
public record DungeonGenerationResult(Dungeon Dungeon, DungeonGenerationIdentity Identity);
