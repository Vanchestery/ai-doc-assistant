using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Entities;
using AiDocAssistant.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiDocAssistant.Tests;

public class RagChatServiceTests
{
    private static readonly Guid SessionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid DocId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task Ask_returns_answer_with_citations_from_search_hits()
    {
        var hits = new List<ChunkHit>
        {
            new(DocId, "invoice.pdf", 0, "Счёт № СЧ-001, сумма 3500 RUB", 0.12)
        };

        var service = CreateService(
            new FakeLlm("По накладной сумма составляет 3500 RUB [1]."),
            new FakeEmbeddings([1f, 0f]),
            new FakeChunkStore(hits),
            new FakeSessionStore(exists: true));

        var reply = await service.AskAsync(SessionId, "Какая сумма по счёту?");

        Assert.Contains("3500", reply.Answer);
        Assert.Single(reply.Citations);
        Assert.Equal("invoice.pdf", reply.Citations[0].DocumentFileName);
        Assert.Equal(0.12, reply.Citations[0].Distance);
    }

    [Fact]
    public async Task Ask_passes_recent_history_to_llm()
    {
        var llm = new FakeLlm("Ответ с учётом контекста.");
        var history = new List<ChatMessage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SessionId = SessionId,
                Role = ChatRole.User,
                Content = "Первый вопрос",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
            },
            new()
            {
                Id = Guid.NewGuid(),
                SessionId = SessionId,
                Role = ChatRole.Assistant,
                Content = "Первый ответ",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            }
        };

        var service = CreateService(
            llm,
            new FakeEmbeddings([1f]),
            new FakeChunkStore([new ChunkHit(DocId, "a.pdf", 0, "текст", 0.1)]),
            new FakeSessionStore(exists: true, history: history));

        await service.AskAsync(SessionId, "Уточняющий вопрос");

        Assert.Contains(llm.LastRequest!.Messages, m => m.Content == "Первый вопрос");
        Assert.Contains(llm.LastRequest.Messages, m => m.Content == "Первый ответ");
    }

    [Fact]
    public async Task Empty_question_throws()
    {
        var service = CreateService(
            new FakeLlm("x"),
            new FakeEmbeddings([1f]),
            new FakeChunkStore([]),
            new FakeSessionStore(exists: true));

        await Assert.ThrowsAsync<ArgumentException>(() => service.AskAsync(SessionId, "   "));
    }

    [Fact]
    public async Task Missing_session_throws()
    {
        var service = CreateService(
            new FakeLlm("x"),
            new FakeEmbeddings([1f]),
            new FakeChunkStore([new ChunkHit(DocId, "a.pdf", 0, "t", 0.1)]),
            new FakeSessionStore(exists: false));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AskAsync(SessionId, "вопрос"));
    }

    [Fact]
    public async Task No_indexed_chunks_throws()
    {
        var service = CreateService(
            new FakeLlm("x"),
            new FakeEmbeddings([1f]),
            new FakeChunkStore([]),
            new FakeSessionStore(exists: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AskAsync(SessionId, "вопрос"));
    }

    private static RagChatService CreateService(
        FakeLlm llm,
        FakeEmbeddings embeddings,
        FakeChunkStore chunks,
        FakeSessionStore sessions) =>
        new(
            llm,
            embeddings,
            chunks,
            sessions,
            new RagOptions { TopK = 3, MaxHistoryMessages = 4 });

    private sealed class FakeLlm : ILlmProvider
    {
        private readonly string _response;
        public LlmRequest? LastRequest { get; private set; }

        public FakeLlm(string response) => _response = response;

        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(new LlmCompletion(_response, "fake-model", 10, 5));
        }
    }

    private sealed class FakeEmbeddings : IEmbeddingProvider
    {
        private readonly float[] _vector;
        public int Dimension => _vector.Length;

        public FakeEmbeddings(float[] vector) => _vector = vector;

        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => _vector.ToArray()).ToList());
    }

    private sealed class FakeChunkStore : IChunkStore
    {
        private readonly IReadOnlyList<ChunkHit> _hits;
        public FakeChunkStore(IReadOnlyList<ChunkHit> hits) => _hits = hits;

        public Task ReplaceForDocumentAsync(Guid documentId, IReadOnlyList<ChunkRecord> chunks, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ChunkHit>> SearchAsync(
            float[] queryEmbedding,
            int topK,
            Guid? documentId = null,
            CancellationToken ct = default)
        {
            var filtered = documentId is null
                ? _hits
                : _hits.Where(h => h.DocumentId == documentId).ToList();

            return Task.FromResult<IReadOnlyList<ChunkHit>>(filtered.Take(topK).ToList());
        }
    }

    private sealed class FakeSessionStore : IChatSessionStore
    {
        private readonly bool _exists;
        private readonly IReadOnlyList<ChatMessage> _history;

        public FakeSessionStore(bool exists, IReadOnlyList<ChatMessage>? history = null)
        {
            _exists = exists;
            _history = history ?? [];
        }

        public Task<Guid> CreateSessionAsync(CancellationToken ct = default) =>
            Task.FromResult(Guid.NewGuid());

        public Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken ct = default) =>
            Task.FromResult(_exists);

        public Task<ChatSession?> GetSessionWithMessagesAsync(Guid sessionId, CancellationToken ct = default) =>
            Task.FromResult<ChatSession?>(null);

        public Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(Guid sessionId, int limit, CancellationToken ct = default) =>
            Task.FromResult(_history);

        public Task AddExchangeAsync(
            Guid sessionId,
            string userContent,
            string assistantContent,
            IReadOnlyList<ChatCitation> citations,
            string model,
            int promptTokens,
            int completionTokens,
            CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
