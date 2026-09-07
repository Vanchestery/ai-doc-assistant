using System.Text.Json;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Agent;
using AiDocAssistant.Core.Entities;
using AiDocAssistant.Core.Services;
using AiDocAssistant.Infrastructure.Persistence;
using AiDocAssistant.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;

namespace AiDocAssistant.Infrastructure.Agent;

/// <summary>generate_report tool: ExtractionResult → xlsx in file storage.</summary>
public sealed class GenerateReportAgentTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;
    private readonly DocumentReportService _report;
    private readonly DocumentReportXlsxWriter _writer;
    private readonly IFileStorage _storage;

    public GenerateReportAgentTool(
        AppDbContext db,
        DocumentReportService report,
        DocumentReportXlsxWriter writer,
        IFileStorage storage)
    {
        _db = db;
        _report = report;
        _writer = writer;
        _storage = storage;
    }

    public string Name => AgentToolNames.GenerateReport;

    public async Task<AgentToolResult> ExecuteAsync(AgentToolInput input, CancellationToken ct = default)
    {
        if (input.DocumentIds.Count == 0)
            throw new ArgumentException("generate_report needs at least 1 documentId.");

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
            .Select(d => new DocumentReportInput(d.Id, d.FileName, d.Extraction!.Json))
            .ToList();

        var dataset = _report.Build(inputs);
        var bytes = _writer.Write(dataset);

        var fileName = $"documents_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        await using var stream = new MemoryStream(bytes);
        var stored = await _storage.SaveAsync(stream, fileName, ct);

        var totalSum = dataset.Documents
            .Where(d => d.TotalAmount is not null)
            .Sum(d => d.TotalAmount!.Value);

        var payload = new
        {
            fileName,
            storagePath = stored.StoragePath,
            sizeBytes = stored.SizeBytes,
            documentCount = dataset.Documents.Count,
            itemCount = dataset.Items.Count,
            totalAmountSum = totalSum,
            sheets = new[] { "Документы", "Позиции" }
        };

        var summary =
            $"Сформирован xlsx-отчёт: {dataset.Documents.Count} док., {dataset.Items.Count} поз., сумма {totalSum:0.00}.";

        return new AgentToolResult(
            JsonSerializer.Serialize(payload, JsonOpts),
            summary);
    }
}
