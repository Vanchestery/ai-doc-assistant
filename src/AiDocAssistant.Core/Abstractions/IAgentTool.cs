namespace AiDocAssistant.Core.Abstractions;

/// <summary>Agent tool (Phase 3). Each tool is a separate implementation in Infrastructure.</summary>
public interface IAgentTool
{
    /// <summary>Name for an explicit API call: reconcile, summarize, generate_report.</summary>
    string Name { get; }

    Task<AgentToolResult> ExecuteAsync(AgentToolInput input, CancellationToken ct = default);
}

public sealed record AgentToolInput(IReadOnlyList<Guid> DocumentIds, string? ParametersJson = null);

public sealed record AgentToolResult(string ResultJson, string? Message = null);
