namespace AiDocAssistant.Core.Entities;

public enum ChatRole
{
    User = 0,
    Assistant = 1
}

/// <summary>Сообщение в сессии чата. У assistant — JSON цитат на использованные чанки.</summary>
public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = null!;

    /// <summary>JSON-массив <see cref="ChatCitation"/> — только для ответов ассистента.</summary>
    public string? CitationsJson { get; set; }

    public string? Model { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ChatSession Session { get; set; } = null!;
}
