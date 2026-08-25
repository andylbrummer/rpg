namespace RPC.Engine.LLM;

/// <summary>
/// Abstraction over LLM API providers (OpenAI, Anthropic, local, etc.)
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Sends a structured prompt and returns the raw text response.
    /// </summary>
    Task<string> CompleteAsync(LLMPrompt prompt, CancellationToken ct = default);

    /// <summary>
    /// Estimated max tokens this provider can handle (including prompt + response).
    /// </summary>
    int ContextWindowSize { get; }
}

public record LLMPrompt(
    string SystemPrompt,
    string UserPrompt,
    float Temperature = 0.7f,
    int MaxTokens = 2048);
