using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Entities;
using AiDocAssistant.Core.Services;
using AiDocAssistant.Infrastructure.Parsing;
using AiDocAssistant.Infrastructure.Persistence;
using ai_doc_assistant.Controllers;
using Microsoft.EntityFrameworkCore;

namespace ai_doc_assistant.Services;

/// <summary>Document list/detail/upload used by the Blazor UI and the REST API.</summary>
public sealed class DocumentProcessingService
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly CompositeDocumentParser _parser;
    private readonly DocumentExtractionService _extraction;
    private readonly DocumentIndexingService _indexing;
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(
        AppDbContext db,
        IFileStorage storage,
        CompositeDocumentParser parser,
        DocumentExtractionService extraction,
        DocumentIndexingService indexing,
        ILogger<DocumentProcessingService> logger)
    {
        _db = db;
        _storage = storage;
        _parser = parser;
        _extraction = extraction;
        _indexing = indexing;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DocumentListItemDto>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Documents
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentListItemDto(
                d.Id, d.FileName, d.Status.ToString(), d.UsedOcr, d.SizeBytes, d.CreatedAt))
            .ToListAsync(ct);

    public async Task<DocumentDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Documents
            .Include(d => d.Extraction)
            .Where(d => d.Id == id)
            .Select(d => new DocumentDetailDto(
                d.Id, d.FileName, d.Status.ToString(), d.UsedOcr, d.SizeBytes, d.CreatedAt, d.Error,
                d.Extraction == null
                    ? null
                    : new ExtractionDto(
                        d.Extraction.Json,
                        d.Extraction.Confidence,
                        d.Extraction.Model,
                        d.Extraction.PromptTokens,
                        d.Extraction.CompletionTokens)))
            .FirstOrDefaultAsync(ct);

    /// <summary>Save the file, parse/OCR, then extract fields via LLM.</summary>
    public async Task<DocumentDetailDto> UploadAsync(
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken ct = default)
    {
        var stored = await _storage.SaveAsync(content, fileName, ct);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType,
            SizeBytes = stored.SizeBytes,
            StoragePath = stored.StoragePath,
            Status = DocumentStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        try
        {
            var parsed = await _parser.ExtractTextAsync(
                _storage.GetFullPath(document.StoragePath), document.FileName, document.ContentType, ct);

            document.ExtractedText = parsed.Text;
            document.UsedOcr = parsed.UsedOcr;
            document.Status = DocumentStatus.Parsed;
            await _db.SaveChangesAsync(ct);

            if (string.IsNullOrWhiteSpace(parsed.Text))
                throw new InvalidOperationException("Could not extract text from the document.");

            var outcome = await _extraction.ExtractAsync(parsed.Text, ct);

            _db.ExtractionResults.Add(new ExtractionResult
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Json = outcome.Json,
                Confidence = outcome.Confidence,
                Model = outcome.Model,
                PromptTokens = outcome.PromptTokens,
                CompletionTokens = outcome.CompletionTokens,
                CreatedAt = DateTimeOffset.UtcNow
            });
            document.Status = DocumentStatus.Extracted;
            await _db.SaveChangesAsync(ct);

            // RAG indexing is best-effort. If the embedder is down (Ollama not running),
            // keep the extraction and index later.
            try
            {
                var indexed = await _indexing.IndexAsync(document.Id, parsed.Text, ct);
                _logger.LogInformation("Document {Id}: indexed {Count} chunks", document.Id, indexed);
            }
            catch (Exception ie) when (ie is not OperationCanceledException)
            {
                _logger.LogWarning(ie, "Indexing document {Id} failed (is the embedder running?)", document.Id);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Failed to process document {Id}", document.Id);
            document.Status = DocumentStatus.Failed;
            document.Error = e.Message;
            await _db.SaveChangesAsync(CancellationToken.None);
        }

        return (await GetByIdAsync(document.Id, ct))!;
    }
}
