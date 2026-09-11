using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class RagRetrievalEvalTests
{
    [Fact]
    public void RunRecordedScenarios_all_pass()
    {
        var cases = RagRetrievalEval.RunRecordedScenarios().ToList();

        Assert.Equal(2, cases.Count);
        Assert.True(cases.All(c => c.Passed), string.Join("; ", cases.Where(c => !c.Passed).Select(c => c.Name)));
    }
}
