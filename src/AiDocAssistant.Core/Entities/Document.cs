namespace AiDocAssistant.Core.Entities;

public class Document
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }

    /// <summary>Относительный путь в файловом хранилище (IFileStorage).</summary>
    public string StoragePath { get; set; } = null!;

    public DocumentStatus Status { get; set; }
    public string? Error { get; set; }

    /// <summary>Извлечённый текст документа. Понадобится для RAG в Фазе 2.</summary>
    public string? ExtractedText { get; set; }

    /// <summary>Текст получен через OCR (скан), а не из текстового слоя.</summary>
    public bool UsedOcr { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ExtractionResult? Extraction { get; set; }
}
