using Pgvector;

namespace AiDocAssistant.Core.Entities;

/// <summary>
/// Фрагмент документа для RAG: текст + его эмбеддинг (вектор в pgvector).
/// Ordinal — порядковый номер чанка внутри документа, нужен для цитат в ответе.
/// </summary>
public class Chunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }

    /// <summary>Порядковый номер чанка в документе (0,1,2...).</summary>
    public int Ordinal { get; set; }

    public string Text { get; set; } = null!;

    /// <summary>Эмбеддинг текста. Тип Vector маппится в колонку vector(N) pgvector.</summary>
    public Vector Embedding { get; set; } = null!;

    /// <summary>Номер страницы источника. Пока null (текст извлекается плоско) — задел на будущее.</summary>
    public int? PageNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
