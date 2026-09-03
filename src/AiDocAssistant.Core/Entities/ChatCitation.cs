namespace AiDocAssistant.Core.Entities;

/// <summary>Source citation in a RAG answer — which document and fragment were used.</summary>
public sealed record ChatCitation(
    Guid DocumentId,
    string DocumentFileName,
    int ChunkOrdinal,
    string Excerpt,
    double Distance);
