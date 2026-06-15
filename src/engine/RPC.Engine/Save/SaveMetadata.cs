namespace RPC.Engine.Save;

/// <summary>
/// Minimal public contract describing a save's identity and version, independent
/// of the full restore payload. Extracted from <see cref="SaveData"/> so callers can
/// inspect compatibility without depending on every feature DTO.
/// </summary>
public readonly record struct SaveMetadata(int SchemaVersion, string? ContentHash)
{
    public static SaveMetadata From(SaveData data) => new(data.SchemaVersion, data.ContentHash);
}

/// <summary>
/// Compatibility checks surfaced when loading a save. Returns human-readable
/// warnings; an empty list means the save is fully compatible with the running build.
/// </summary>
public static class SaveCompatibility
{
    public static IReadOnlyList<string> CheckContentHash(SaveMetadata metadata, string? expectedContentHash)
    {
        if (string.IsNullOrEmpty(expectedContentHash) || metadata.ContentHash == expectedContentHash)
            return Array.Empty<string>();

        return new[]
        {
            $"Content hash mismatch: save was created with '{metadata.ContentHash ?? "(none)"}', " +
            $"current is '{expectedContentHash}'. Loading anyway."
        };
    }
}
