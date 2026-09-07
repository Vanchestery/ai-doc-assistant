using System.Text.Json;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Agent;
using AiDocAssistant.Core.Entities;
using AiDocAssistant.Core.Services;
using AiDocAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiDocAssistant.Infrastructure.Agent;

/// <summary>summarize tool: ExtractionResult → LLM → text summary.</summary>
public sealed class SummarizeAgentTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;
    private readonly DocumentSummarizeService _summarize;

    public SummarizeAgentTool(AppDbContext db, DocumentSummarizeService summarize)
    {
        _db = db;
        _summarize = summarize;
    }

    public string Name => AgentToolNames.Summarize;

    public async Task<AgentToolResult> ExecuteAsync(AgentToolInput input, CancellationToken ct = default)
    {
        if (input.DocumentIds.Count == 0)
            throw new ArgumentException("summarize needs at least 1 documentId.");

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

        var inputs = documents
            .Select(d => new DocumentSummaryInput(d.Id, d.FileName, d.Extraction!.Json))
            .ToList();

        var outcome = await _summarize.SummarizeAsync(inputs, ct);

        var totalSum = outcome.Documents
            .Where(d => d.TotalAmount is not null)
            .Sum(d => d.TotalAmount!.Value);

        var payload = new
        {
            summary = outcome.Summary,
            documentCount = outcome.Documents.Count,
            totalAmountSum = totalSum,
            model = outcome.Model,
            promptTokens = outcome.PromptTokens,
            completionTokens = outcome.CompletionTokens,
            documents = outcome.Documents.Select(d => new
            {
                d.DocumentId,
                d.FileName,
                d.DocType,
                d.Number,
                d.Date,
                counterparty = d.CounterpartyName,
                d.TotalAmount,
                d.Currency
            })
        };

        return new AgentToolResult(
            JsonSerializer.Serialize(payload, JsonOpts),
            outcome.Summary);
    }
}
