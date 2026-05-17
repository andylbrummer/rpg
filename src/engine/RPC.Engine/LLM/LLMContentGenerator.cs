using System.Text.Json;
using RPC.Engine.Campaign;

namespace RPC.Engine.LLM;

/// <summary>
/// Orchestrates LLM-driven campaign config generation with caching,
/// validation, retry logic, and offline fallback.
/// </summary>
public class LLMContentGenerator
{
    private readonly ILLMProvider? _provider;
    private readonly GenerationCache _cache;
    private readonly PromptBuilder _promptBuilder;
    private readonly int _maxRetries;

    public LLMContentGenerator(
        ILLMProvider? provider,
        PromptBuilder promptBuilder,
        GenerationCache? cache = null,
        int maxRetries = 3)
    {
        _provider = provider;
        _promptBuilder = promptBuilder;
        _cache = cache ?? new GenerationCache();
        _maxRetries = maxRetries;
    }

    public bool IsOnline => _provider != null;

    /// <summary>
    /// Generates a CampaignConfig from six rolls. Uses cache if available.
    /// Falls back to hand-authored base config if LLM is unavailable.
    /// </summary>
    public async Task<CampaignConfig> GenerateCampaignAsync(
        int[] rolls,
        string contentHash,
        CampaignConfig? fallbackConfig = null,
        CancellationToken ct = default)
    {
        var cacheKey = _cache.GetKey(rolls, contentHash);
        if (_cache.TryGet(cacheKey, out var cachedJson))
        {
            var cached = TryParseConfig(cachedJson);
            if (cached != null) return cached;
        }

        if (!IsOnline)
        {
            return fallbackConfig ?? CreateDefaultConfig(rolls);
        }

        var prompt = _promptBuilder.BuildCampaignPrompt(rolls, fallbackConfig);
        string? lastError = null;

        for (int attempt = 0; attempt < _maxRetries; attempt++)
        {
            try
            {
                var response = await _provider!.CompleteAsync(prompt, ct);
                var json = ExtractJson(response);
                var config = TryParseConfig(json);
                string? validationError = null;

                if (config != null && ValidateConfig(config, out validationError))
                {
                    _cache.Put(cacheKey, json);
                    return config;
                }

                lastError = validationError ?? "Validation failed";
                prompt = prompt with
                {
                    UserPrompt = prompt.UserPrompt + $"\n\nPrevious attempt failed: {lastError}. Fix the issue and try again."
                };
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        // All retries exhausted — use fallback
        return fallbackConfig ?? CreateDefaultConfig(rolls);
    }

    /// <summary>
    /// Generates an enhanced epilogue from the action log.
    /// Falls back to template epilogue if LLM is unavailable.
    /// </summary>
    public async Task<string> GenerateEpilogueAsync(GameState state, CancellationToken ct = default)
    {
        if (!IsOnline)
            return EpilogueGenerator.Generate(state);

        var prompt = _promptBuilder.BuildEpiloguePrompt(state);
        try
        {
            var response = await _provider!.CompleteAsync(prompt, ct);
            return response.Trim();
        }
        catch
        {
            return EpilogueGenerator.Generate(state);
        }
    }

    private static CampaignConfig? TryParseConfig(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CampaignConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static bool ValidateConfig(CampaignConfig config, out string? error)
    {
        if (string.IsNullOrEmpty(config.Patron))
        {
            error = "Missing patron";
            return false;
        }
        if (string.IsNullOrEmpty(config.Mastermind))
        {
            error = "Missing mastermind";
            return false;
        }
        if (config.EvidenceChain.Count == 0)
        {
            error = "Empty evidence chain";
            return false;
        }
        error = null;
        return true;
    }

    private static string ExtractJson(string response)
    {
        // Extract JSON from markdown code blocks if present
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return response[start..(end + 1)];
        }
        return response;
    }

    private static CampaignConfig CreateDefaultConfig(int[] rolls)
    {
        // Deterministic fallback using rolls as seeds
        var factions = new[] { "bureau", "convocation", "cartography", "stillness", "inkblood" };
        var schemes = new[] { SchemeType.BloomHarvest, SchemeType.EngineSeizure, SchemeType.CascadeFailure, SchemeType.TheResurrection, SchemeType.ManufacturedCrisis, SchemeType.TheVault };
        var complications = new[] { ComplicationType.BloomSiege, ComplicationType.TitheCollapse, ComplicationType.OpenWar, ComplicationType.ErraticEngine, ComplicationType.MissingTeam, ComplicationType.ClosingPasses };

        var rng = new Random(rolls[0] * 100000 + rolls[1] * 10000 + rolls[2] * 1000 + rolls[3] * 100 + rolls[4] * 10 + rolls[5]);

        var shuffledFactions = factions.OrderBy(_ => rng.Next()).ToArray();
        var patron = shuffledFactions[0];
        var threat = shuffledFactions[1];
        var mastermind = shuffledFactions[2];
        var wildCard = shuffledFactions[3];

        return new CampaignConfig
        {
            Patron = patron,
            Threat = threat,
            Mastermind = mastermind,
            Scheme = schemes[rng.Next(schemes.Length)],
            WildCard = wildCard,
            Complication = complications[rng.Next(complications.Length)],
            EvidenceChain = new List<string> { "clue_1", "clue_2", "clue_3" },
            FactionTimelines = new Dictionary<string, FactionTimeline>
            {
                [mastermind] = new FactionTimeline(5, 10),
                [threat] = new FactionTimeline(8, 12),
                [wildCard] = new FactionTimeline(10, 15)
            },
            NpcCasting = new Dictionary<string, string>(),
            WildcardTrigger = new WildcardTrigger(wildCard, 20)
        };
    }
}
