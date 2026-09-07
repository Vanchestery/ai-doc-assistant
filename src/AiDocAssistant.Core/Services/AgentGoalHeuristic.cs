using AiDocAssistant.Core.Agent;

namespace AiDocAssistant.Core.Services;

/// <summary>
/// Offline baseline for goal→tool (eval without an LLM). Mirrors the expected router logic.
/// </summary>
public static class AgentGoalHeuristic
{
    public static string? ResolveTool(string goal, int documentCount)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return null;

        var g = goal.ToLowerInvariant();

        if (ContainsAny(g, "свер", "расхож", "reconcil", "compare"))
            return documentCount >= 2 ? AgentToolNames.Reconcile : null;

        if (ContainsAny(g, "сводк", "summar", "summary"))
            return AgentToolNames.Summarize;

        if (ContainsAny(g, "отчёт", "отчет", "excel", "xlsx", "report"))
            return AgentToolNames.GenerateReport;

        return null;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
