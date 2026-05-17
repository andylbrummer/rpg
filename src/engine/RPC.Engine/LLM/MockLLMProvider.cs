namespace RPC.Engine.LLM;

/// <summary>
/// Mock LLM provider for testing. Returns deterministic responses based on prompt hash.
/// </summary>
public class MockLLMProvider : ILLMProvider
{
    public int ContextWindowSize => 16000;

    private readonly Func<LLMPrompt, string> _responseFactory;

    public MockLLMProvider(Func<LLMPrompt, string>? responseFactory = null)
    {
        _responseFactory = responseFactory ?? (prompt =>
        {
            // Return a minimal valid CampaignConfig JSON
            return """
            {
              "patron": "bureau",
              "threat": "convocation",
              "mastermind": "inkblood",
              "scheme": "TheVault",
              "wildCard": "cartography",
              "complication": "WildCardArrival",
              "evidenceChain": ["seal_broken", "ink_stains", "guardian_defeated"],
              "factionTimelines": {
                "inkblood": { "preparing": 5, "executing": 10 },
                "convocation": { "preparing": 8, "executing": 12 },
                "cartography": { "preparing": 10, "executing": 15 }
              },
              "npcCasting": {},
              "wildcardTrigger": { "factionId": "cartography", "turnThreshold": 20 }
            }
            """;
        });
    }

    public Task<string> CompleteAsync(LLMPrompt prompt, CancellationToken ct = default)
    {
        return Task.FromResult(_responseFactory(prompt));
    }
}
