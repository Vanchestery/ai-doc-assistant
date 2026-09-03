namespace AiDocAssistant.Core.Entities;

public class Document
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }

    /// <summary>Relative path in file storage (IFileStorage).</summary>
    public string StoragePath { get; set; } = null!;

    public DocumentStatus Status { get; set; }
    public string? Error { get; set; }

    /// <summary>Extracted document text. Needed for RAG in Phase 2.</summary>
    public string? ExtractedText { get; set; }

    /// <summary>Text came from OCR (scan), not from a text layer.</summary>
    public bool UsedOcr { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ExtractionResult? Extraction { get; set; }
}
