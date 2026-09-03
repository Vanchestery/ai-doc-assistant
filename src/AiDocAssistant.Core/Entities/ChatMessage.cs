namespace AiDocAssistant.Core.Entities;

public enum ChatRole
{
    User = 0,
    Assistant = 1
}

/// <summary>A message in a chat session. Assistant messages may include JSON citations of used chunks.</summary>
public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = null!;

    /// <summary>JSON array of <see cref="ChatCitation"/> — assistant replies only.</summary>
    public string? CitationsJson { get; set; }

    public string? Model { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ChatSession Session { get; set; } = null!;
}
