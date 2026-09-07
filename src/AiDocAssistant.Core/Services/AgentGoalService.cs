using AiDocAssistant.Core.Entities;

namespace AiDocAssistant.Core.Services;

/// <summary>Goal mode: the LLM picks a tool, then AgentTaskService runs it.</summary>
public sealed class AgentGoalService
{
    private readonly AgentGoalRouterService _router;
    private readonly AgentTaskService _tasks;
    private readonly AgentToolRegistry _tools;

    public AgentGoalService(
        AgentGoalRouterService router,
        AgentTaskService tasks,
        AgentToolRegistry tools)
    {
        _router = router;
        _tasks = tasks;
        _tools = tools;
    }

    public async Task<AgentGoalOutcome> RunAsync(
        string goal,
        IReadOnlyList<Guid> documentIds,
        CancellationToken ct = default)
    {
        var routing = await _router.RouteAsync(goal, documentIds.Count, _tools.Names, ct);

        var action = await _tasks.RunAsync(
            routing.Tool,
            documentIds,
            new AgentTaskRunContext(goal.Trim(), routing.Reasoning),
            ct);

        return new AgentGoalOutcome(goal.Trim(), routing, action);
    }
}

public sealed record AgentTaskRunContext(string Goal, string RoutingReason);

public sealed record AgentGoalOutcome(
    string Goal,
    GoalRoutingOutcome Routing,
    AgentAction Action);
