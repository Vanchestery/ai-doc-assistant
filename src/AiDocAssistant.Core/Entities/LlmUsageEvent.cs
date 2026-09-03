namespace AiDocAssistant.Core.Entities;

/// <summary>Телеметрия одного LLM-вызова (Фаза 4).</summary>
public class LlmUsageEvent
{
    public Guid Id { get; set; }

    /// <summary>extraction, rag_chat, summarize, goal_router.</summary>
    public string Operation { get; set; } = null!;

    public string Model { get; set; } = null!;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public long LatencyMs { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
