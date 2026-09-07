using System.Text.Json;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Entities;

namespace AiDocAssistant.Core.Services;

/// <summary>Run an agent tool by explicit name and persist the AgentAction.</summary>
public sealed class AgentTaskService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IAgentTaskStore _store;
    private readonly AgentToolRegistry _tools;

    public AgentTaskService(IAgentTaskStore store, AgentToolRegistry tools)
    {
        _store = store;
        _tools = tools;
    }

    public Task<AgentAction> RunAsync(
        string tool,
        IReadOnlyList<Guid> documentIds,
        CancellationToken ct = default) =>
        RunAsync(tool, documentIds, context: null, ct);

    public async Task<AgentAction> RunAsync(
        string tool,
        IReadOnlyList<Guid> documentIds,
        AgentTaskRunContext? context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tool))
            throw new ArgumentException("Tool is not specified.", nameof(tool));

        if (documentIds.Count == 0)
            throw new ArgumentException("At least one documentId is required.", nameof(documentIds));

        var inputJson = context is null
            ? JsonSerializer.Serialize(new { documentIds }, JsonOpts)
            : JsonSerializer.Serialize(new
            {
                documentIds,
                goal = context.Goal,
                routingReason = context.RoutingReason
            }, JsonOpts);
        var action = await _store.CreateAsync(tool.Trim(), inputJson, ct);

        action.Status = AgentActionStatus.Running;
        await _store.SaveAsync(action, ct);

        try
        {
            var agentTool = _tools.Get(tool);
            var result = await agentTool.ExecuteAsync(new AgentToolInput(documentIds), ct);

            action.Status = AgentActionStatus.Completed;
            action.ResultJson = result.ResultJson;
            action.CompletedAt = DateTimeOffset.UtcNow;
            action.Error = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            action.Status = AgentActionStatus.Failed;
            action.Error = ex.Message;
            action.CompletedAt = DateTimeOffset.UtcNow;
        }

        await _store.SaveAsync(action, ct);
        return action;
    }

    public Task<AgentAction?> GetAsync(Guid id, CancellationToken ct = default) =>
        _store.GetAsync(id, ct);
}

/// <summary>Registry of IAgentTool implementations by name.</summary>
public sealed class AgentToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _tools;

    public AgentToolRegistry(IEnumerable<IAgentTool> tools) =>
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    public IAgentTool Get(string name)
    {
        if (!_tools.TryGetValue(name, out var tool))
            throw new KeyNotFoundException(
                $"Tool '{name}' was not found. Available: {string.Join(", ", _tools.Keys)}.");

        return tool;
    }

    public IReadOnlyCollection<string> Names => _tools.Keys.ToList();
}
