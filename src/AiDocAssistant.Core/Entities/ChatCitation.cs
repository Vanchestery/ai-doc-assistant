namespace AiDocAssistant.Core.Entities;

/// <summary>Цитата источника в ответе RAG — какой документ и фрагмент использованы.</summary>
public sealed record ChatCitation(
    Guid DocumentId,
    string DocumentFileName,
    int ChunkOrdinal,
    string Excerpt,
    double Distance);
