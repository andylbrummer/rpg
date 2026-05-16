namespace RPC.Engine.Town;

public enum RumorTruthStatus
{
    True,
    Outdated,
    Planted
}

public enum RumorVerificationSource
{
    AshmouthBroker,
    InkbloodScribe,
    HollowContact,
    Firsthand
}

public record RumorDef(
    string Id,
    string Text,
    RumorTruthStatus TruthStatus,
    string? RelatedContentId = null,
    string? RelatedFactionId = null);

public record TownRumor(
    string Id,
    string Text,
    RumorTruthStatus TruthStatus,
    bool Verified,
    bool? VerificationResult = null,
    string? RelatedContentId = null,
    string? RelatedFactionId = null);
