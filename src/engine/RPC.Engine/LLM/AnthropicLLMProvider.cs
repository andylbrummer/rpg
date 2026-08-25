using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPC.Engine.LLM;

/// <summary>
/// Anthropic Messages API provider. POSTs to /v1/messages with x-api-key auth,
/// retries transient failures (429, 5xx) with exponential backoff, and surfaces
/// the assistant's text content. The orchestrator (LLMContentGenerator) owns
/// schema validation and template-epilogue fallback.
/// </summary>
public class AnthropicLLMProvider : ILLMProvider, IDisposable
{
    public const string DefaultModel = "claude-sonnet-4-6";
    public const string DefaultBaseUrl = "https://api.anthropic.com";
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _model;
    private readonly int _maxTransportRetries;

    public int ContextWindowSize { get; }

    public AnthropicLLMProvider(
        string apiKey,
        string model = DefaultModel,
        string baseUrl = DefaultBaseUrl,
        int contextWindowSize = 200_000,
        int maxTransportRetries = 3,
        HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Anthropic API key is required", nameof(apiKey));

        _model = model;
        ContextWindowSize = contextWindowSize;
        _maxTransportRetries = Math.Max(1, maxTransportRetries);

        if (httpClient is null)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            _ownsHttp = true;
        }
        else
        {
            _http = httpClient;
            _ownsHttp = false;
            if (_http.BaseAddress is null) _http.BaseAddress = new Uri(baseUrl);
        }

        _http.DefaultRequestHeaders.Remove("x-api-key");
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Remove("anthropic-version");
        _http.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
    }

    /// <summary>
    /// Construct from environment when ANTHROPIC_API_KEY is set; returns null otherwise so
    /// callers can fall back to MockLLMProvider or offline templates.
    /// </summary>
    public static AnthropicLLMProvider? FromEnvironment(HttpClient? httpClient = null)
    {
        var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) return null;
        var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL");
        return new AnthropicLLMProvider(
            apiKey: key,
            model: string.IsNullOrWhiteSpace(model) ? DefaultModel : model,
            httpClient: httpClient);
    }

    public async Task<string> CompleteAsync(LLMPrompt prompt, CancellationToken ct = default)
    {
        var body = new MessagesRequest(
            Model: _model,
            MaxTokens: prompt.MaxTokens,
            Temperature: prompt.Temperature,
            System: prompt.SystemPrompt,
            Messages: new[] { new Message("user", prompt.UserPrompt) });

        Exception? lastError = null;

        for (int attempt = 0; attempt < _maxTransportRetries; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await _http.PostAsJsonAsync("/v1/messages", body, SerializerOptions, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsRetryableException(ex))
            {
                lastError = ex;
                if (attempt + 1 >= _maxTransportRetries) break;
                await BackoffAsync(attempt, ct);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    var parsed = await response.Content.ReadFromJsonAsync<MessagesResponse>(SerializerOptions, ct);
                    var text = ExtractText(parsed);
                    if (string.IsNullOrWhiteSpace(text))
                        throw new InvalidOperationException("Anthropic response contained no text content");
                    return text;
                }

                var detail = await SafeReadBodyAsync(response, ct);
                var error = new HttpRequestException(
                    $"Anthropic API returned {(int)response.StatusCode} {response.ReasonPhrase}: {detail}");

                if (!IsRetryableStatus(response.StatusCode))
                    throw error;

                lastError = error;
                if (attempt + 1 >= _maxTransportRetries) break;
                await BackoffAsync(attempt, ct);
            }
        }

        throw new HttpRequestException(
            $"Anthropic API call failed after {_maxTransportRetries} attempts",
            lastError);
    }

    private static bool IsRetryableStatus(System.Net.HttpStatusCode status) =>
        (int)status == 429 || (int)status >= 500;

    private static bool IsRetryableException(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or IOException;

    private static Task BackoffAsync(int attempt, CancellationToken ct)
    {
        var delayMs = (int)Math.Min(8_000, 250 * Math.Pow(2, attempt));
        return Task.Delay(delayMs, ct);
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadAsStringAsync(ct); }
        catch { return "<unreadable>"; }
    }

    private static string ExtractText(MessagesResponse? response)
    {
        if (response?.Content is null) return string.Empty;
        var sb = new System.Text.StringBuilder();
        foreach (var block in response.Content)
        {
            if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
                sb.Append(block.Text);
        }
        return sb.ToString();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
        GC.SuppressFinalize(this);
    }

    private record MessagesRequest(
        string Model,
        int MaxTokens,
        float Temperature,
        string? System,
        IReadOnlyList<Message> Messages);

    private record Message(string Role, string Content);

    private record MessagesResponse(
        [property: JsonPropertyName("content")] IReadOnlyList<ContentBlock>? Content,
        [property: JsonPropertyName("stop_reason")] string? StopReason);

    private record ContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);
}
