namespace AiDocAssistant.Core.Entities;

public class ExtractionResult
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    /// <summary>Извлечённые поля документа (jsonb).</summary>
    public string Json { get; set; } = null!;

    /// <summary>Самооценка модели 0..1, из поля confidence в ответе.</summary>
    public double? Confidence { get; set; }

    // Телеметрия вызова LLM — база для метрик стоимости в Фазе 4
    public string Model { get; set; } = null!;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
