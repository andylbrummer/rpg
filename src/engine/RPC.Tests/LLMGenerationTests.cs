using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.LLM;

namespace RPC.Tests;

public class LLMGenerationTests : IDisposable
{
    private readonly string _cacheDir;

    public LLMGenerationTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), $"rpc_llm_test_{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, true);
    }

    [Fact]
    public async Task GenerateCampaign_Online_ReturnsValidConfig()
    {
        var provider = new MockLLMProvider();
        var index = new ContentIndex { ContentHash = "abc" };
        var builder = new PromptBuilder(index);
        var cache = new GenerationCache(_cacheDir);
        var generator = new LLMContentGenerator(provider, builder, cache);

        var config = await generator.GenerateCampaignAsync(new[] { 1, 2, 3, 4, 5, 6 }, "abc");

        Assert.NotNull(config);
        Assert.False(string.IsNullOrEmpty(config.Patron));
        Assert.False(string.IsNullOrEmpty(config.Mastermind));
        Assert.NotEmpty(config.EvidenceChain);
    }

    [Fact]
    public async Task GenerateCampaign_Offline_ReturnsFallbackConfig()
    {
        var index = new ContentIndex { ContentHash = "abc" };
        var builder = new PromptBuilder(index);
        var cache = new GenerationCache(_cacheDir);
        var generator = new LLMContentGenerator(null, builder, cache);

        var config = await generator.GenerateCampaignAsync(new[] { 1, 2, 3, 4, 5, 6 }, "abc");

        Assert.NotNull(config);
        Assert.False(string.IsNullOrEmpty(config.Patron));
    }

    [Fact]
    public async Task GenerateCampaign_CachesResult()
    {
        var provider = new MockLLMProvider();
        var index = new ContentIndex { ContentHash = "abc" };
        var builder = new PromptBuilder(index);
        var cache = new GenerationCache(_cacheDir);
        var generator = new LLMContentGenerator(provider, builder, cache);

        var rolls = new[] { 1, 2, 3, 4, 5, 6 };
        var config1 = await generator.GenerateCampaignAsync(rolls, "abc");
        var config2 = await generator.GenerateCampaignAsync(rolls, "abc");

        Assert.Equal(config1.Patron, config2.Patron);
        Assert.Equal(config1.Mastermind, config2.Mastermind);
    }

    [Fact]
    public async Task GenerateEpilogue_Online_ReturnsText()
    {
        var provider = new MockLLMProvider(p => "The campaign ended in darkness.");
        var index = new ContentIndex();
        var builder = new PromptBuilder(index);
        var generator = new LLMContentGenerator(provider, builder);

        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = "inkblood",
            Patron = "bureau",
            Scheme = SchemeType.TheVault,
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };

        var text = await generator.GenerateEpilogueAsync(state);
        Assert.Equal("The campaign ended in darkness.", text);
    }

    [Fact]
    public async Task GenerateEpilogue_Offline_FallsBackToTemplate()
    {
        var index = new ContentIndex();
        var builder = new PromptBuilder(index);
        var generator = new LLMContentGenerator(null, builder);

        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = "inkblood",
            Patron = "bureau",
            Scheme = SchemeType.TheVault,
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };

        var text = await generator.GenerateEpilogueAsync(state);
        Assert.Contains("inkblood", text);
    }
}
