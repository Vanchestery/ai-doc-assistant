using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class LlmCostEstimatorTests
{
    [Fact]
    public void Estimate_calculates_prompt_and_completion_cost()
    {
        var estimator = new LlmCostEstimator(new LlmPricingOptions
        {
            PromptUsdPer1M = 1m,
            CompletionUsdPer1M = 2m
        });

        var cost = estimator.Estimate(promptTokens: 1_000_000, completionTokens: 500_000);

        Assert.Equal(2m, cost);
    }
}
