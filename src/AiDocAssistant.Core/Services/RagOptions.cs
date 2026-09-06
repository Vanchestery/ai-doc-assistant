namespace AiDocAssistant.Core.Services;

/// <summary>RAG chat settings: how many chunks to retrieve and how much history to send to the LLM.</summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    /// <summary>Number of nearest chunks from pgvector (top-K).</summary>
    public int TopK { get; set; } = 5;

    /// <summary>Last N session messages to include in the prompt (user/assistant pairs).</summary>
    public int MaxHistoryMessages { get; set; } = 10;
}
