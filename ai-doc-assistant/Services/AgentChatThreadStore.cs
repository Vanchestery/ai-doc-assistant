using System.Collections.Concurrent;

namespace ai_doc_assistant.Services;

/// <summary>In-memory agent chat threads (demo): one message ≈ one goal/tool run.</summary>
public sealed class AgentChatThreadStore
{
    private readonly ConcurrentDictionary<Guid, List<AgentChatTurn>> _threads = new();

    public Guid Create()
    {
        var id = Guid.NewGuid();
        _threads[id] = [];
        return id;
    }

    public bool Exists(Guid threadId) => _threads.ContainsKey(threadId);

    public IReadOnlyList<AgentChatTurn> GetTurns(Guid threadId)
    {
        if (!_threads.TryGetValue(threadId, out var turns))
            return [];

        lock (turns)
            return turns.ToList();
    }

    public void Append(Guid threadId, AgentChatTurn turn)
    {
        var turns = _threads.GetOrAdd(threadId, _ => []);
        lock (turns)
            turns.Add(turn);
    }
}

public sealed record AgentChatTurn(string Goal, Guid TaskId, DateTimeOffset CreatedAt);
