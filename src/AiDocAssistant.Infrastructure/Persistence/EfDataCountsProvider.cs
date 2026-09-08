using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiDocAssistant.Infrastructure.Persistence;

/// <summary>Row counts for the metrics dashboard.</summary>
public sealed class EfDataCountsProvider : IDataCountsProvider
{
    private readonly AppDbContext _db;

    public EfDataCountsProvider(AppDbContext db) => _db = db;

    public async Task<DataCountsSnapshot> GetCountsAsync(CancellationToken ct = default) =>
        new(
            await _db.Documents.CountAsync(ct),
            await _db.Documents.CountAsync(d => d.Status == DocumentStatus.Extracted, ct),
            await _db.ChatSessions.CountAsync(ct),
            await _db.AgentActions.CountAsync(ct),
            await _db.LlmUsageEvents.CountAsync(ct));
}
