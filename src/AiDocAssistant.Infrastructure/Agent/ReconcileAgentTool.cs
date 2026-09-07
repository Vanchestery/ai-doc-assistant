using System.Text.Json;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Agent;
using AiDocAssistant.Core.Entities;
using AiDocAssistant.Core.Services;
using AiDocAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiDocAssistant.Infrastructure.Agent;

/// <summary>reconcile tool: load ExtractionResult from the DB and run a deterministic compare.</summary>
public sealed class ReconcileAgentTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;
    private readonly DocumentReconcileService _reconcile;

    public ReconcileAgentTool(AppDbContext db, DocumentReconcileService reconcile)
    {
        _db = db;
        _reconcile = reconcile;
    }

    public string Name => AgentToolNames.Reconcile;

    public async Task<AgentToolResult> ExecuteAsync(AgentToolInput input, CancellationToken ct = default)
    {
        if (input.DocumentIds.Count < 2)
            throw new ArgumentException("reconcile needs at least 2 documentIds.");

        var documents = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Extraction)
            .Where(d => input.DocumentIds.Contains(d.Id))
            .ToListAsync(ct);

        var missing = input.DocumentIds.Except(documents.Select(d => d.Id)).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException($"Documents not found: {string.Join(", ", missing)}");

        var notExtracted = documents
            .Where(d => d.Extraction is null || d.Status != DocumentStatus.Extracted)
            .Select(d => d.FileName)
            .ToList();

        if (notExtracted.Count > 0)
            throw new InvalidOperationException(
                $"No extracted data for: {string.Join(", ", notExtracted)}. Upload via POST /api/documents first.");

        var snapshots = documents
            .Select(d => new DocumentExtractionSnapshot(d.Id, d.FileName, d.Extraction!.Json))
            .ToList();

        var outcome = _reconcile.Reconcile(snapshots);

        var payload = new
        {
            outcome.Summary,
            outcome.HasDiscrepancies,
            documents = outcome.Documents.Select(d => new
            {
                d.DocumentId,
                d.FileName,
                d.Fields
            }),
            discrepancies = outcome.Discrepancies.Select(d => new
            {
                d.Field,
                d.Description,
                values = d.Values.Select(v => new
                {
                    v.DocumentId,
                    v.FileName,
                    v.Value
                })
            })
        };

        return new AgentToolResult(
            JsonSerializer.Serialize(payload, JsonOpts),
            outcome.Summary);
    }
}
