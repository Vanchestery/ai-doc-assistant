using ai_doc_assistant.Services;
using AiDocAssistant.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ai_doc_assistant.Controllers;

/// <summary>Classic form POST for Agent Chat (SSR, no Blazor circuit).</summary>
[IgnoreAntiforgeryToken]
public class AgentChatAskController : Controller
{
    private readonly AgentGoalService _goals;
    private readonly AgentChatThreadStore _threads;

    public AgentChatAskController(AgentGoalService goals, AgentChatThreadStore threads)
    {
        _goals = goals;
        _threads = threads;
    }

    [HttpPost("/agent/chat/{threadId:guid}/ask")]
    public async Task<IActionResult> Ask(
        Guid threadId,
        string? goal,
        List<Guid>? documentIds,
        CancellationToken ct)
    {
        if (!_threads.Exists(threadId))
            return Redirect("/agent/chat");

        documentIds ??= [];

        try
        {
            var outcome = await _goals.RunAsync(goal ?? "", documentIds, ct);
            _threads.Append(
                threadId,
                new AgentChatTurn(outcome.Goal, outcome.Action.Id, DateTimeOffset.UtcNow));

            return Redirect($"/agent/chat/{threadId}");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Redirect($"/agent/chat/{threadId}?error={Uri.EscapeDataString(e.Message)}");
        }
    }
}
