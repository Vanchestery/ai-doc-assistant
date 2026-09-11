using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class ExtractionGoldenEvalTests
{
    private const string Expected =
        """
        {
          "doc_type": "счет",
          "number": "2026-041",
          "date": "2026-07-15",
          "counterparty": { "name": "OOO KofePoint", "inn": "7707654321" },
          "total_amount": 112700.0,
          "currency": "RUB"
        }
        """;

    [Fact]
    public void Matching_json_passes()
    {
        var outcome = ExtractionGoldenEval.Compare("test", Expected, Expected);

        Assert.True(outcome.Case.Passed);
        Assert.Equal(outcome.ComparedFields, outcome.MatchedFields);
    }

    [Fact]
    public void Wrong_total_fails()
    {
        const string actual =
            """
            {
              "doc_type": "счет",
              "number": "2026-041",
              "date": "2026-07-15",
              "counterparty": { "name": "OOO KofePoint", "inn": "7707654321" },
              "total_amount": 999.0,
              "currency": "RUB"
            }
            """;

        var outcome = ExtractionGoldenEval.Compare("test", Expected, actual);

        Assert.False(outcome.Case.Passed);
        Assert.Contains("total_amount", outcome.Case.Detail);
    }
}
