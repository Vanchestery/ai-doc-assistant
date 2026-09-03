using AiDocAssistant.Core.Entities;

namespace AiDocAssistant.Core.Abstractions;

/// <summary>Persistence for agent tasks (AgentAction).</summary>
public interface IAgentTaskStore
{
    Task<AgentAction> CreateAsync(string tool, string inputJson, CancellationToken ct = default);

    Task<AgentAction?> GetAsync(Guid id, CancellationToken ct = default);

    Task SaveAsync(AgentAction action, CancellationToken ct = default);
}
