namespace AiDocAssistant.Core.Services;

/// <summary>Estimate LLM call cost from token counts (configurable rates).</summary>
public sealed class LlmCostEstimator
{
    private readonly decimal _promptUsdPer1M;
    private readonly decimal _completionUsdPer1M;

    public LlmCostEstimator(LlmPricingOptions options)
    {
        _promptUsdPer1M = options.PromptUsdPer1M;
        _completionUsdPer1M = options.CompletionUsdPer1M;
    }

    public decimal Estimate(int promptTokens, int completionTokens) =>
        promptTokens / 1_000_000m * _promptUsdPer1M
        + completionTokens / 1_000_000m * _completionUsdPer1M;
}

public sealed class LlmPricingOptions
{
    public const string SectionName = "LlmPricing";

    /// <summary>USD per 1M prompt tokens (DeepSeek-chat ballpark).</summary>
    public decimal PromptUsdPer1M { get; set; } = 0.27m;

    /// <summary>USD per 1M completion tokens.</summary>
    public decimal CompletionUsdPer1M { get; set; } = 1.10m;
}
