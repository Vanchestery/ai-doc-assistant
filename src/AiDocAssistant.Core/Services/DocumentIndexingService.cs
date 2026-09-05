using AiDocAssistant.Core.Abstractions;

namespace AiDocAssistant.Core.Services;

/// <summary>
/// Index a document for RAG: text -> chunks -> embeddings -> store.
/// Orchestration lives in Core; chunker/embedder/store sit behind interfaces.
/// </summary>
public sealed class DocumentIndexingService
{
    private readonly ITextChunker _chunker;
    private readonly IEmbeddingProvider _embeddings;
    private readonly IChunkStore _store;

    public DocumentIndexingService(ITextChunker chunker, IEmbeddingProvider embeddings, IChunkStore store)
    {
        _chunker = chunker;
        _embeddings = embeddings;
        _store = store;
    }

    /// <summary>Returns the number of indexed chunks.</summary>
    public async Task<int> IndexAsync(Guid documentId, string text, CancellationToken ct = default)
    {
        var chunks = _chunker.Chunk(text);
        if (chunks.Count == 0)
            return 0;

        var vectors = await _embeddings.EmbedAsync(chunks.Select(c => c.Text).ToArray(), ct);
        if (vectors.Count != chunks.Count)
            throw new InvalidOperationException(
                $"Embedder returned {vectors.Count} vectors for {chunks.Count} chunks.");

        var records = chunks
            .Zip(vectors, (c, v) => new ChunkRecord(c.Index, c.Text, v))
            .ToArray();

        await _store.ReplaceForDocumentAsync(documentId, records, ct);
        return records.Length;
    }
}
