namespace AiDocAssistant.Core.Entities;

/// <summary>Задача агента: вызов tool (reconcile, summarize, generate_report).</summary>
public class AgentAction
{
    public Guid Id { get; set; }

    /// <summary>Имя tool: reconcile, summarize, generate_report.</summary>
    public string Tool { get; set; } = null!;

    /// <summary>Вход задачи (JSON: documentIds и параметры).</summary>
    public string InputJson { get; set; } = null!;

    /// <summary>Результат tool (JSON).</summary>
    public string? ResultJson { get; set; }

    public AgentActionStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
