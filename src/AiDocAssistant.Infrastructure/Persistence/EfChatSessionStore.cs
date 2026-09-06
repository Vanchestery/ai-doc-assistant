using System.Text.Json;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiDocAssistant.Infrastructure.Persistence;

/// <summary>Chat sessions and message history in Postgres via EF Core.</summary>
public sealed class EfChatSessionStore : IChatSessionStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;

    public EfChatSessionStore(AppDbContext db) => _db = db;

    public async Task<Guid> CreateSessionAsync(CancellationToken ct = default)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session.Id;
    }

    public Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken ct = default) =>
        _db.ChatSessions.AnyAsync(s => s.Id == sessionId, ct);

    public Task<ChatSession?> GetSessionWithMessagesAsync(Guid sessionId, CancellationToken ct = default) =>
        _db.ChatSessions
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

    public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(
        Guid sessionId,
        int limit,
        CancellationToken ct = default)
    {
        if (limit <= 0)
            return [];

        return await _db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddExchangeAsync(
        Guid sessionId,
        string userContent,
        string assistantContent,
        IReadOnlyList<ChatCitation> citations,
        string model,
        int promptTokens,
        int completionTokens,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        _db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = ChatRole.User,
            Content = userContent,
            CreatedAt = now
        });

        _db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = ChatRole.Assistant,
            Content = assistantContent,
            CitationsJson = JsonSerializer.Serialize(citations, JsonOpts),
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            CreatedAt = now.AddMilliseconds(1)
        });

        await _db.SaveChangesAsync(ct);
    }
}
