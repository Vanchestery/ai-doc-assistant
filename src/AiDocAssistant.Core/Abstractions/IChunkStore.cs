namespace AiDocAssistant.Core.Abstractions;

/// <summary>
/// Chunk store with vector search. Implementation is pgvector on top of EF Core.
/// The interface uses float[] and does not pull the Vector type into Core logic.
/// </summary>
public interface IChunkStore
{
    /// <summary>
    /// Replace all chunks of a document with new ones (idempotent re-index):
    /// old chunks are deleted, new ones are saved. Re-upload does not create duplicates.
    /// </summary>
    Task ReplaceForDocumentAsync(Guid documentId, IReadOnlyList<ChunkRecord> chunks, CancellationToken ct = default);

    /// <summary>
    /// Top-K chunks nearest to the query by cosine distance.
    /// documentId is an optional filter for a single document.
    /// </summary>
    Task<IReadOnlyList<ChunkHit>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        Guid? documentId = null,
        CancellationToken ct = default);
}

/// <summary>Chunk ready to persist: text plus its embedding.</summary>
public sealed record ChunkRecord(int Ordinal, string Text, float[] Embedding);

/// <summary>Search result: chunk, document name, and distance (smaller is closer).</summary>
public sealed record ChunkHit(
    Guid DocumentId,
    string DocumentFileName,
    int Ordinal,
    string Text,
    double Distance);
