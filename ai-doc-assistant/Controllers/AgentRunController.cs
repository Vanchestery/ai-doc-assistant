using AiDocAssistant.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ai_doc_assistant.Controllers;

/// <summary>Classic form POST for the Agent page (no Blazor/SignalR).</summary>
[IgnoreAntiforgeryToken]
public class AgentRunController : Controller
{
    private readonly AgentTaskService _tasks;
    private readonly AgentGoalService _goals;

    public AgentRunController(AgentTaskService tasks, AgentGoalService goals)
    {
        _tasks = tasks;
        _goals = goals;
    }

    [HttpPost("/agent/run")]
    public async Task<IActionResult> Run(
        string? mode,
        string? tool,
        string? goal,
        List<Guid>? documentIds,
        CancellationToken ct)
    {
        documentIds ??= [];

        try
        {
            if (string.Equals(mode, "goal", StringComparison.OrdinalIgnoreCase))
            {
                var outcome = await _goals.RunAsync(goal ?? "", documentIds, ct);
                return Redirect($"/agent/{outcome.Action.Id}");
            }

            var action = await _tasks.RunAsync(tool ?? "", documentIds, ct);
            return Redirect($"/agent/{action.Id}");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Redirect($"/agent?error={Uri.EscapeDataString(e.Message)}");
        }
    }
}
