using System.Text.Json;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ai_doc_assistant.Controllers;

[ApiController]
[Route("api/chat")]
[IgnoreAntiforgeryToken]
public class ChatController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IChatSessionStore _sessions;
    private readonly RagChatService _rag;

    public ChatController(IChatSessionStore sessions, RagChatService rag)
    {
        _sessions = sessions;
        _rag = rag;
    }

    /// <summary>Create a new chat session.</summary>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(CreateSessionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateSessionDto>> CreateSession(CancellationToken ct)
    {
        var id = await _sessions.CreateSessionAsync(ct);
        return CreatedAtAction(nameof(GetSession), new { id }, new CreateSessionDto(id, DateTimeOffset.UtcNow));
    }

    /// <summary>Session message history with citations.</summary>
    [HttpGet("sessions/{id:guid}")]
    [ProducesResponseType(typeof(ChatSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatSessionDto>> GetSession(Guid id, CancellationToken ct)
    {
        var session = await _sessions.GetSessionWithMessagesAsync(id, ct);
        if (session is null)
            return NotFound();

        var messages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto(
                m.Id,
                m.Role.ToString(),
                m.Content,
                ParseCitations(m.CitationsJson),
                m.Model,
                m.PromptTokens,
                m.CompletionTokens,
                m.CreatedAt))
            .ToList();

        return new ChatSessionDto(session.Id, session.CreatedAt, messages);
    }

    /// <summary>Ask a question over indexed documents (RAG).</summary>
    [HttpPost("sessions/{id:guid}/messages")]
    [ProducesResponseType(typeof(AskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AskResponseDto>> Ask(Guid id, [FromBody] AskRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question cannot be empty.");

        try
        {
            var reply = await _rag.AskAsync(id, request.Question, request.DocumentId, ct);
            return new AskResponseDto(
                reply.Answer,
                reply.Citations.Select(c => new CitationDto(
                    c.DocumentId,
                    c.DocumentFileName,
                    c.ChunkOrdinal,
                    c.Excerpt,
                    c.Distance)).ToList(),
                reply.Model,
                reply.PromptTokens,
                reply.CompletionTokens);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(e.Message);
        }
    }

    private static IReadOnlyList<CitationDto>? ParseCitations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<List<CitationDto>>(json, JsonOpts);
    }
}

public record CreateSessionDto(Guid Id, DateTimeOffset CreatedAt);

public record AskRequestDto(string Question, Guid? DocumentId = null);

public record AskResponseDto(
    string Answer,
    IReadOnlyList<CitationDto> Citations,
    string Model,
    int PromptTokens,
    int CompletionTokens);

public record ChatSessionDto(Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<ChatMessageDto> Messages);

public record ChatMessageDto(
    Guid Id,
    string Role,
    string Content,
    IReadOnlyList<CitationDto>? Citations,
    string? Model,
    int? PromptTokens,
    int? CompletionTokens,
    DateTimeOffset CreatedAt);

public record CitationDto(
    Guid DocumentId,
    string DocumentFileName,
    int ChunkOrdinal,
    string Excerpt,
    double Distance);
