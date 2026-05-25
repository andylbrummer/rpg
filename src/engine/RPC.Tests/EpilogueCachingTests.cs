using System.Collections.Generic;
using System.Threading.Tasks;
using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.LLM;

namespace RPC.Tests;

public class EpilogueCachingTests
{
    private static LLMContentGenerator OfflineGenerator()
        => new LLMContentGenerator(null, new PromptBuilder(new ContentIndex { ContentHash = "x" }));

    [Fact]
    public void ResolveEpilogue_CachesResult_StableAcrossStateChanges()
    {
        var gs = new GameState();
        var first = gs.ResolveEpilogue();
        Assert.NotNull(gs.CachedEpilogue);

        // Mutate state after caching — the resolved epilogue must not regenerate.
        gs.ActionLog.Add(new ActionLogEntry(1, 1, "combat", "character_died",
            new Dictionary<string, string> { { "characterName", "Casualty" } }));

        var second = gs.ResolveEpilogue();
        Assert.Same(first, second);
    }

    [Fact]
    public void SetCachedEpilogue_Overrides_Template()
    {
        var gs = new GameState();
        gs.SetCachedEpilogue("CUSTOM EPILOGUE");
        Assert.Equal("CUSTOM EPILOGUE", gs.ResolveEpilogue());
    }

    [Fact]
    public void Reset_ClearsEpilogueCache()
    {
        var gs = new GameState();
        gs.ResolveEpilogue();
        Assert.NotNull(gs.CachedEpilogue);

        gs.Reset();
        Assert.Null(gs.CachedEpilogue);
    }

    [Fact]
    public async Task GenerateEpilogueAsync_Offline_ReturnsTemplateAndCaches()
    {
        var gs = new GameState();
        var result = await OfflineGenerator().GenerateEpilogueAsync(gs);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.Equal(result, gs.CachedEpilogue);
        Assert.Equal(EpilogueGenerator.Generate(gs), result); // matches the template fallback
    }

    [Fact]
    public async Task GenerateEpilogueAsync_CacheHit_ReturnsCachedWithoutRegenerating()
    {
        var gs = new GameState();
        gs.SetCachedEpilogue("PRECACHED");

        var result = await OfflineGenerator().GenerateEpilogueAsync(gs);
        Assert.Equal("PRECACHED", result);
    }
}
