using AiDocAssistant.Core.Evals;
using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class GoldenFixturesTests
{
    [Fact]
    public void Load_invoice_a_expected_contains_total()
    {
        var json = GoldenFixtures.Load("invoice-a.expected.json");
        Assert.Contains("112700", json);
    }

    [Fact]
    public void Recorded_golden_invoice_a_passes()
    {
        var outcome = ExtractionGoldenEval.Compare(
            "test",
            GoldenFixtures.Load("invoice-a.expected.json"),
            GoldenFixtures.Load("invoice-a.actual.json"));

        Assert.True(outcome.Case.Passed, outcome.Case.Detail);
    }
}
