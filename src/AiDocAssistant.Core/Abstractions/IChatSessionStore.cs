using AiDocAssistant.Core.Entities;

namespace AiDocAssistant.Core.Abstractions;

/// <summary>Persistence for chat sessions and message history.</summary>
public interface IChatSessionStore
{
    Task<Guid> CreateSessionAsync(CancellationToken ct = default);

    Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken ct = default);

    Task<ChatSession?> GetSessionWithMessagesAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Last N messages in chronological order (for LLM context).</summary>
    Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(Guid sessionId, int limit, CancellationToken ct = default);

    Task AddExchangeAsync(
        Guid sessionId,
        string userContent,
        string assistantContent,
        IReadOnlyList<ChatCitation> citations,
        string model,
        int promptTokens,
        int completionTokens,
        CancellationToken ct = default);
}
