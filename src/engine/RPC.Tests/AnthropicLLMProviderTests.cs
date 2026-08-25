using System.Net;
using System.Text;
using System.Text.Json;
using RPC.Engine.LLM;

namespace RPC.Tests;

public class AnthropicLLMProviderTests
{
    [Fact]
    public void Constructor_ThrowsWhenApiKeyMissing()
    {
        Assert.Throws<ArgumentException>(() => new AnthropicLLMProvider(""));
        Assert.Throws<ArgumentException>(() => new AnthropicLLMProvider("   "));
    }

    [Fact]
    public void FromEnvironment_ReturnsNullWhenKeyAbsent()
    {
        var prev = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        try
        {
            Assert.Null(AnthropicLLMProvider.FromEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", prev);
        }
    }

    [Fact]
    public async Task CompleteAsync_ReturnsAggregatedText()
    {
        var handler = new ScriptedHandler(_ => StubOk("Hello world"));
        using var http = new HttpClient(handler);
        using var provider = new AnthropicLLMProvider("test-key", httpClient: http);

        var text = await provider.CompleteAsync(new LLMPrompt("sys", "user"));

        Assert.Equal("Hello world", text);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("test-key", handler.LastHeaders!["x-api-key"]);
        Assert.Equal("2023-06-01", handler.LastHeaders!["anthropic-version"]);
    }

    [Fact]
    public async Task CompleteAsync_RetriesOn500ThenSucceeds()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") },
            StubOk("ok-after-retry")
        });
        var handler = new ScriptedHandler(_ => responses.Dequeue());
        using var http = new HttpClient(handler);
        using var provider = new AnthropicLLMProvider("k", maxTransportRetries: 3, httpClient: http);

        var text = await provider.CompleteAsync(new LLMPrompt("s", "u"));

        Assert.Equal("ok-after-retry", text);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task CompleteAsync_ThrowsAfterExhaustingRetries()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("rate-limited")
        });
        using var http = new HttpClient(handler);
        using var provider = new AnthropicLLMProvider("k", maxTransportRetries: 2, httpClient: http);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.CompleteAsync(new LLMPrompt("s", "u")));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task CompleteAsync_ThrowsOn4xxImmediately()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("bad key")
        });
        using var http = new HttpClient(handler);
        using var provider = new AnthropicLLMProvider("k", maxTransportRetries: 3, httpClient: http);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.CompleteAsync(new LLMPrompt("s", "u")));
        Assert.Contains("401", ex.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CompleteAsync_SendsPromptInRequestBody()
    {
        string? capturedBody = null;
        var handler = new ScriptedHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return StubOk("ack");
        });
        using var http = new HttpClient(handler);
        using var provider = new AnthropicLLMProvider("k", model: "claude-test", httpClient: http);

        await provider.CompleteAsync(new LLMPrompt(
            SystemPrompt: "be terse",
            UserPrompt: "ping",
            Temperature: 0.2f,
            MaxTokens: 512));

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal("claude-test", root.GetProperty("model").GetString());
        Assert.Equal(512, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.2f, root.GetProperty("temperature").GetSingle(), 3);
        Assert.Equal("be terse", root.GetProperty("system").GetString());
        var msg = root.GetProperty("messages")[0];
        Assert.Equal("user", msg.GetProperty("role").GetString());
        Assert.Equal("ping", msg.GetProperty("content").GetString());
    }

    [Fact]
    public async Task LLMContentGenerator_FallsBackWhenProviderThrows()
    {
        var alwaysFails = new ThrowingProvider();
        var index = new ContentIndex { ContentHash = "h" };
        var builder = new PromptBuilder(index);
        var cacheDir = Path.Combine(Path.GetTempPath(), $"rpc_anth_test_{Guid.NewGuid()}");
        try
        {
            var generator = new LLMContentGenerator(alwaysFails, builder, new GenerationCache(cacheDir));
            var config = await generator.GenerateCampaignAsync(new[] { 1, 2, 3, 4, 5, 6 }, "h");

            Assert.NotNull(config);
            Assert.False(string.IsNullOrEmpty(config.Patron));
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    private static HttpResponseMessage StubOk(string text)
    {
        var payload = $$"""
        {
          "id": "msg_1",
          "type": "message",
          "role": "assistant",
          "content": [ { "type": "text", "text": {{JsonSerializer.Serialize(text)}} } ],
          "stop_reason": "end_turn"
        }
        """;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int Calls { get; private set; }
        public Dictionary<string, string>? LastHeaders { get; private set; }

        public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastHeaders = request.Headers
                .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class ThrowingProvider : ILLMProvider
    {
        public int ContextWindowSize => 1000;
        public Task<string> CompleteAsync(LLMPrompt prompt, CancellationToken ct = default)
            => throw new HttpRequestException("transport down");
    }
}
