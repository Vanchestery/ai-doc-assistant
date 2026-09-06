namespace AiDocAssistant.Infrastructure.Llm;

/// <summary>
/// Settings for the OpenAI-compatible /v1/embeddings provider.
/// Default is local Ollama with bge-m3 (1024-dim, multilingual, good on Russian).
/// ApiKey is only needed for cloud providers.
/// </summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "bge-m3";
    public string? ApiKey { get; set; }

    /// <summary>Vector dimension. Must match the vector(N) column in the database.</summary>
    public int Dimension { get; set; } = 1024;
}
