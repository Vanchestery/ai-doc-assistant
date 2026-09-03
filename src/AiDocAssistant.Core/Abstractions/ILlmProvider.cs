namespace AiDocAssistant.Core.Abstractions;

/// <summary>
/// Model-agnostic LLM access. Implementations: DeepSeek (Infrastructure);
/// later — Ollama or any other OpenAI-compatible provider.
/// </summary>
public interface ILlmProvider
{
    Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken ct = default);
}

public sealed record LlmMessage(string Role, string Content)
{
    public static LlmMessage System(string content) => new("system", content);
    public static LlmMessage User(string content) => new("user", content);
    public static LlmMessage Assistant(string content) => new("assistant", content);
}

public sealed record LlmRequest(
    IReadOnlyList<LlmMessage> Messages,
    bool JsonMode = false,
    double Temperature = 0.2,
    int? MaxTokens = null,
    string? Operation = null);

/// <summary>Response plus telemetry (tokens are needed for cost metrics, Phase 4).</summary>
public sealed record LlmCompletion(
    string Content,
    string Model,
    int PromptTokens,
    int CompletionTokens);
