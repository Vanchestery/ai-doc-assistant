using AiDocAssistant.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ai_doc_assistant.Controllers;

/// <summary>Classic form POST for the Chat page (no Blazor/SignalR).</summary>
[IgnoreAntiforgeryToken]
public class ChatAskController : Controller
{
    private readonly RagChatService _rag;

    public ChatAskController(RagChatService rag) => _rag = rag;

    [HttpPost("/chat/{id:guid}/ask")]
    public async Task<IActionResult> Ask(Guid id, string? question, Guid? documentId, CancellationToken ct)
    {
        try
        {
            await _rag.AskAsync(id, question ?? "", documentId, ct);
            return Redirect(ChatUrl(id, documentId, error: null));
        }
        catch (KeyNotFoundException)
        {
            return Redirect("/chat");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Redirect(ChatUrl(id, documentId, e.Message));
        }
    }

    private static string ChatUrl(Guid id, Guid? documentId, string? error)
    {
        var url = $"/chat/{id}";
        var query = new List<string>();
        if (documentId is not null)
            query.Add($"documentId={documentId}");
        if (!string.IsNullOrWhiteSpace(error))
            query.Add($"error={Uri.EscapeDataString(error)}");
        return query.Count == 0 ? url : $"{url}?{string.Join("&", query)}";
    }
}
