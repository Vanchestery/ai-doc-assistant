namespace AiDocAssistant.Core.Entities;

public class ExtractionResult
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    /// <summary>Extracted document fields (jsonb).</summary>
    public string Json { get; set; } = null!;

    /// <summary>Model self-score 0..1, from the confidence field in the response.</summary>
    public double? Confidence { get; set; }

    // LLM call telemetry — basis for cost metrics in Phase 4
    public string Model { get; set; } = null!;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
