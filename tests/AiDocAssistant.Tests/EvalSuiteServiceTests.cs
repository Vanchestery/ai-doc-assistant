using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class EvalSuiteServiceTests
{
    [Fact]
    public void RunAll_passes_golden_cases()
    {
        var service = new EvalSuiteService(new DocumentReconcileService());
        var result = service.RunAll();

        Assert.True(result.AllPassed, string.Join("; ", result.Cases.Where(c => !c.Passed).Select(c => c.Name)));
        Assert.Equal(14, result.Cases.Count);
        Assert.Equal(100.0, result.GoldenFieldAccuracyPercent);
    }
}
