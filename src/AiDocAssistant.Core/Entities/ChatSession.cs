namespace AiDocAssistant.Core.Entities;

/// <summary>Диалог пользователя с ассистентом по документам (Фаза 2 RAG).</summary>
public class ChatSession
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
