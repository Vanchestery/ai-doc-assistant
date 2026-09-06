using System.Text;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Entities;
using AiDocAssistant.Core.Llm;

namespace AiDocAssistant.Core.Services;

/// <summary>
/// RAG chat: question -> embedding -> pgvector search -> LLM answer with citations.
/// Citations come from search hits (ground truth), not from the model's word.
/// </summary>
public sealed class RagChatService
{
    private const int MaxExcerptChars = 300;

    private const string SystemPrompt =
        """
        Ты — ассистент по документам бэк-офиса (накладные, счета, акты, отчёты).
        Отвечай на вопрос пользователя ТОЛЬКО на основе приведённых фрагментов документов.
        Если в фрагментах нет данных для ответа — честно скажи, что информации недостаточно.
        При ссылке на источник используй номера фрагментов [1], [2] и т.д.
        Отвечай на русском языке, кратко и по делу.
        """;

    private readonly ILlmProvider _llm;
    private readonly IEmbeddingProvider _embeddings;
    private readonly IChunkStore _chunks;
    private readonly IChatSessionStore _sessions;
    private readonly RagOptions _options;

    public RagChatService(
        ILlmProvider llm,
        IEmbeddingProvider embeddings,
        IChunkStore chunks,
        IChatSessionStore sessions,
        RagOptions? options = null)
    {
        _llm = llm;
        _embeddings = embeddings;
        _chunks = chunks;
        _sessions = sessions;
        _options = options ?? new RagOptions();
    }

    public async Task<RagChatReply> AskAsync(
        Guid sessionId,
        string question,
        Guid? documentId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question cannot be empty.", nameof(question));

        if (!await _sessions.SessionExistsAsync(sessionId, ct))
            throw new KeyNotFoundException($"Session {sessionId} was not found.");

        var trimmedQuestion = question.Trim();

        var queryVectors = await _embeddings.EmbedAsync([trimmedQuestion], ct);
        if (queryVectors.Count != 1)
            throw new InvalidOperationException("Embedder must return one vector for the question.");

        var hits = await _chunks.SearchAsync(
            queryVectors[0], _options.TopK, documentId, ct);

        if (hits.Count == 0)
            throw new InvalidOperationException(
                documentId is null
                    ? "The index has no chunks. Upload documents first."
                    : "This document has no indexed chunks.");

        var history = await _sessions.GetRecentMessagesAsync(
            sessionId, _options.MaxHistoryMessages, ct);

        var messages = new List<LlmMessage> { LlmMessage.System(SystemPrompt) };
        foreach (var msg in history)
        {
            messages.Add(msg.Role switch
            {
                ChatRole.User => LlmMessage.User(msg.Content),
                ChatRole.Assistant => LlmMessage.Assistant(msg.Content),
                _ => throw new InvalidOperationException($"Unknown chat role: {msg.Role}")
            });
        }

        messages.Add(LlmMessage.User(BuildUserPrompt(trimmedQuestion, hits)));

        var completion = await _llm.CompleteAsync(
            new LlmRequest(messages, JsonMode: false, Temperature: 0.2, Operation: LlmOperations.RagChat), ct);

        var citations = hits
            .Select(h => new ChatCitation(
                h.DocumentId,
                h.DocumentFileName,
                h.Ordinal,
                TruncateExcerpt(h.Text),
                h.Distance))
            .ToList();

        await _sessions.AddExchangeAsync(
            sessionId,
            trimmedQuestion,
            completion.Content,
            citations,
            completion.Model,
            completion.PromptTokens,
            completion.CompletionTokens,
            ct);

        return new RagChatReply(
            completion.Content,
            citations,
            completion.Model,
            completion.PromptTokens,
            completion.CompletionTokens);
    }

    private static string BuildUserPrompt(string question, IReadOnlyList<ChunkHit> hits)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Фрагменты документов:");
        sb.AppendLine();

        for (var i = 0; i < hits.Count; i++)
        {
            var hit = hits[i];
            sb.AppendLine($"[{i + 1}] Документ: {hit.DocumentFileName}, фрагмент #{hit.Ordinal}");
            sb.AppendLine(hit.Text.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("Вопрос пользователя:");
        sb.Append(question);
        return sb.ToString();
    }

    private static string TruncateExcerpt(string text) =>
        text.Length <= MaxExcerptChars ? text.Trim() : text[..MaxExcerptChars].Trim() + "…";
}

/// <summary>RAG chat reply: assistant text plus citations of the chunks that were used.</summary>
public sealed record RagChatReply(
    string Answer,
    IReadOnlyList<ChatCitation> Citations,
    string Model,
    int PromptTokens,
    int CompletionTokens);
