namespace AiDocAssistant.Core.Entities;

/// <summary>User conversation with the assistant over documents (Phase 2 RAG).</summary>
public class ChatSession
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
