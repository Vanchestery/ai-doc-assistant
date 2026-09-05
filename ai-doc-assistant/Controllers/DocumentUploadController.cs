using ai_doc_assistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace ai_doc_assistant.Controllers;

/// <summary>Classic form POST for the Documents page (no Blazor/SignalR).</summary>
[IgnoreAntiforgeryToken]
public class DocumentUploadController : Controller
{
    private readonly DocumentProcessingService _documents;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DocumentUploadController> _logger;

    public DocumentUploadController(
        DocumentProcessingService documents,
        IWebHostEnvironment env,
        ILogger<DocumentUploadController> logger)
    {
        _documents = documents;
        _env = env;
        _logger = logger;
    }

    [HttpPost("/documents/upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        _logger.LogInformation("Form upload started, file={Name}, size={Size}",
            file?.FileName, file?.Length);

        if (file is null || file.Length == 0)
            return BadRequest("Empty file.");

        await using var stream = file.OpenReadStream();
        var dto = await _documents.UploadAsync(stream, file.FileName, file.ContentType, ct);
        return Redirect($"/documents/{dto.Id}");
    }

    [HttpPost("/documents/upload-path")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadPath(string? path, CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return BadRequest("File path not found.");

        _logger.LogInformation("Path upload started, path={Path}", path);

        await using var stream = System.IO.File.OpenRead(path);
        var dto = await _documents.UploadAsync(stream, Path.GetFileName(path), null, ct);
        return Redirect($"/documents/{dto.Id}");
    }
}
