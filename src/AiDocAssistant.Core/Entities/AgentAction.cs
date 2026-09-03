namespace AiDocAssistant.Core.Entities;

/// <summary>Agent task: a tool call (reconcile, summarize, generate_report).</summary>
public class AgentAction
{
    public Guid Id { get; set; }

    /// <summary>Tool name: reconcile, summarize, generate_report.</summary>
    public string Tool { get; set; } = null!;

    /// <summary>Task input (JSON: documentIds and parameters).</summary>
    public string InputJson { get; set; } = null!;

    /// <summary>Tool result (JSON).</summary>
    public string? ResultJson { get; set; }

    public AgentActionStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
