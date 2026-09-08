using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Entities;
using AiDocAssistant.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AiDocAssistant.Infrastructure.Persistence;

/// <summary>LLM usage telemetry in Postgres via EF Core.</summary>
public sealed class EfLlmUsageStore : ILlmUsageStore
{
    private readonly AppDbContext _db;

    public EfLlmUsageStore(AppDbContext db) => _db = db;

    public async Task RecordAsync(LlmUsageEvent usage, CancellationToken ct = default)
    {
        _db.LlmUsageEvents.Add(usage);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<LlmUsageSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var rows = await _db.LlmUsageEvents.AsNoTracking().ToListAsync(ct);

        var latencies = rows.Select(r => r.LatencyMs).ToList();

        var byOperation = rows
            .GroupBy(r => r.Operation)
            .Select(g =>
            {
                var opLatencies = g.Select(x => x.LatencyMs).ToList();
                return new LlmUsageByOperation(
                    g.Key,
                    g.Count(),
                    g.Sum(x => x.PromptTokens),
                    g.Sum(x => x.CompletionTokens),
                    g.Sum(x => x.LatencyMs),
                    LatencyStats.Percentile(opLatencies, 50),
                    LatencyStats.Percentile(opLatencies, 95),
                    g.Sum(x => x.EstimatedCostUsd));
            })
            .OrderBy(x => x.Operation)
            .ToList();

        return new LlmUsageSummary(
            rows.Count,
            rows.Sum(r => r.PromptTokens),
            rows.Sum(r => r.CompletionTokens),
            rows.Sum(r => r.LatencyMs),
            LatencyStats.Percentile(latencies, 50),
            LatencyStats.Percentile(latencies, 95),
            rows.Sum(r => r.EstimatedCostUsd),
            byOperation);
    }
}
