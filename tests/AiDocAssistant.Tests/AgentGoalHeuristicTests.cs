using AiDocAssistant.Core.Agent;
using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class AgentGoalHeuristicTests
{
    [Theory]
    [InlineData("Сверь счета", 2, AgentToolNames.Reconcile)]
    [InlineData("Сделай сводку", 1, AgentToolNames.Summarize)]
    [InlineData("Отчёт в Excel", 1, AgentToolNames.GenerateReport)]
    public void ResolveTool_matches_expected(string goal, int docCount, string expected)
    {
        Assert.Equal(expected, AgentGoalHeuristic.ResolveTool(goal, docCount));
    }

    [Fact]
    public void Reconcile_with_one_document_returns_null()
    {
        Assert.Null(AgentGoalHeuristic.ResolveTool("Сверь счета", 1));
    }
}
