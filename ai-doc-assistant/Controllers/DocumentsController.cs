using ai_doc_assistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace ai_doc_assistant.Controllers;

[ApiController]
[Route("api/documents")]
[IgnoreAntiforgeryToken]
public class DocumentsController : ControllerBase
{
    private readonly DocumentProcessingService _documents;

    public DocumentsController(DocumentProcessingService documents) => _documents = documents;

    /// <summary>Upload a document: save -> parse/OCR -> LLM field extraction.</summary>
    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(DocumentDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DocumentDetailDto>> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Empty file.");

        await using var stream = file.OpenReadStream();
        var dto = await _documents.UploadAsync(stream, file.FileName, file.ContentType, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpGet]
    public Task<IReadOnlyList<DocumentListItemDto>> GetAll(CancellationToken ct) =>
        _documents.GetAllAsync(ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _documents.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : dto;
    }
}

public record DocumentListItemDto(
    Guid Id, string FileName, string Status, bool UsedOcr, long SizeBytes, DateTimeOffset CreatedAt);

public record DocumentDetailDto(
    Guid Id, string FileName, string Status, bool UsedOcr, long SizeBytes, DateTimeOffset CreatedAt,
    string? Error, ExtractionDto? Extraction);

public record ExtractionDto(
    string Json, double? Confidence, string Model, int PromptTokens, int CompletionTokens);
