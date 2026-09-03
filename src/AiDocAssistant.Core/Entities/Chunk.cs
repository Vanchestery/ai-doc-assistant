using Pgvector;

namespace AiDocAssistant.Core.Entities;

/// <summary>
/// Document fragment for RAG: text plus its embedding (vector in pgvector).
/// Ordinal is the chunk index within the document, used for citations in the answer.
/// </summary>
public class Chunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }

    /// <summary>Ordinal of the chunk in the document (0, 1, 2...).</summary>
    public int Ordinal { get; set; }

    public string Text { get; set; } = null!;

    /// <summary>Text embedding. The Vector type maps to a pgvector vector(N) column.</summary>
    public Vector Embedding { get; set; } = null!;

    /// <summary>Source page number. Null for now (text is extracted flat) — reserved for later.</summary>
    public int? PageNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
