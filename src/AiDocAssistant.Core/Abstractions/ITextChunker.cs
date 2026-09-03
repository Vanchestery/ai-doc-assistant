namespace AiDocAssistant.Core.Abstractions;

/// <summary>
/// Split document text into fragments (chunks) for embeddings and vector search.
/// The strategy is behind the interface so it can be swapped or compared in evals (Phase 4).
/// </summary>
public interface ITextChunker
{
    IReadOnlyList<TextChunk> Chunk(string text, ChunkingOptions? options = null);
}

/// <summary>A text fragment. Index is the ordinal within the document (for citations).</summary>
public sealed record TextChunk(int Index, string Text)
{
    public int CharCount => Text.Length;
}

/// <summary>
/// Chunking parameters. Size and overlap are in characters, not tokens:
/// no external tokenizer, model-agnostic (see DECISIONS.md #12).
/// </summary>
public sealed record ChunkingOptions(int MaxChars = 1000, int Overlap = 150)
{
    public static ChunkingOptions Default { get; } = new();
}
