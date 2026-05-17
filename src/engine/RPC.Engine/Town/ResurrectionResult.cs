using RPC.Engine.Character;

namespace RPC.Engine;

public record ResurrectionResult(
    bool Success,
    string? Error = null,
    int GoldCost = 0,
    int TitheTokenCost = 0,
    int StatLossCount = 0,
    bool BranchLocked = false,
    CharacterState? Character = null);
