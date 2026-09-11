using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Agent;
using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class AgentGoalRouterServiceTests
{
    private static readonly string[] Tools =
    [
        AgentToolNames.Reconcile,
        AgentToolNames.Summarize,
        AgentToolNames.GenerateReport
    ];

    [Theory]
    [InlineData("Сверь эти счета и покажи расхождения", 2, AgentToolNames.Reconcile)]
    [InlineData("Сделай краткую сводку по документам", 2, AgentToolNames.Summarize)]
    [InlineData("Сформируй отчёт в Excel", 1, AgentToolNames.GenerateReport)]
    public async Task RouteAsync_picks_expected_tool(string goal, int docCount, string expectedTool)
    {
        var llm = new FakeLlm($$"""{"tool":"{{expectedTool}}","reasoning":"тест"}""");
        var router = new AgentGoalRouterService(llm);

        var outcome = await router.RouteAsync(goal, docCount, Tools);

        Assert.Equal(expectedTool, outcome.Tool);
        Assert.Equal(1, llm.Calls);
    }

    [Fact]
    public async Task Reconcile_with_one_document_throws()
    {
        var router = new AgentGoalRouterService(new FakeLlm(
            """{"tool":"reconcile","reasoning":"сверка"}"""));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.RouteAsync("сверь", 1, Tools));

        Assert.Contains("at least 2", ex.Message);
    }

    [Fact]
    public async Task Unknown_tool_from_llm_throws()
    {
        var router = new AgentGoalRouterService(new FakeLlm(
            """{"tool":"delete_all","reasoning":"x"}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.RouteAsync("удали всё", 2, Tools));
    }

    private sealed class FakeLlm : ILlmProvider
    {
        private readonly string _response;
        public int Calls { get; private set; }

        public FakeLlm(string response) => _response = response;

        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        {
            Calls++;
            Assert.True(request.JsonMode);
            return Task.FromResult(new LlmCompletion(_response, "fake-model", 40, 10));
        }
    }
}
