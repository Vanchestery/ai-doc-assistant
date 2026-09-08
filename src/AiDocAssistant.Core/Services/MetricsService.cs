using AiDocAssistant.Core.Abstractions;

namespace AiDocAssistant.Core.Services;

/// <summary>Dashboard snapshot: LLM usage, row counts, and offline evals.</summary>
public sealed class MetricsService
{
    private readonly ILlmUsageStore _llmUsage;
    private readonly EvalSuiteService _evals;
    private readonly IDataCountsProvider _counts;

    public MetricsService(
        ILlmUsageStore llmUsage,
        EvalSuiteService evals,
        IDataCountsProvider counts)
    {
        _llmUsage = llmUsage;
        _evals = evals;
        _counts = counts;
    }

    public async Task<MetricsSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var llm = await _llmUsage.GetSummaryAsync(ct);
        var data = await _counts.GetCountsAsync(ct);
        var evals = _evals.RunAll();

        return new MetricsSummary(llm, data, evals);
    }

    public EvalSuiteResult RunEvals() => _evals.RunAll();
}

public sealed record MetricsSummary(
    LlmUsageSummary Llm,
    DataCountsSnapshot Data,
    EvalSuiteResult Evals);
