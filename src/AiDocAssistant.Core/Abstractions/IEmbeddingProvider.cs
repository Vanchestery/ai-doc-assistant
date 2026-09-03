namespace AiDocAssistant.Core.Abstractions;

/// <summary>
/// Model-agnostic embedding generation. Implementation is an OpenAI-compatible endpoint
/// (local Ollama by default); the provider can be swapped behind this interface.
/// Returns float[] — no coupling to the pgvector Vector type (see DECISIONS.md #13).
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Embedding dimension of the model (must match the vector(N) column).</summary>
    int Dimension { get; }

    /// <summary>Embeddings for a batch of texts, in the same order as the input.</summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
