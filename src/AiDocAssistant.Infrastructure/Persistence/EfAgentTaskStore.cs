using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiDocAssistant.Infrastructure.Persistence;

/// <summary>Agent tasks (AgentAction) in Postgres via EF Core.</summary>
public sealed class EfAgentTaskStore : IAgentTaskStore
{
    private readonly AppDbContext _db;

    public EfAgentTaskStore(AppDbContext db) => _db = db;

    public async Task<AgentAction> CreateAsync(string tool, string inputJson, CancellationToken ct = default)
    {
        var action = new AgentAction
        {
            Id = Guid.NewGuid(),
            Tool = tool,
            InputJson = inputJson,
            Status = AgentActionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AgentActions.Add(action);
        await _db.SaveChangesAsync(ct);
        return action;
    }

    public Task<AgentAction?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.AgentActions.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task SaveAsync(AgentAction action, CancellationToken ct = default)
    {
        _db.AgentActions.Update(action);
        await _db.SaveChangesAsync(ct);
    }
}
