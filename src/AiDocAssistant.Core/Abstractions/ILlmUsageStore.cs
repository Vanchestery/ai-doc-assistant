using AiDocAssistant.Core.Entities;

namespace AiDocAssistant.Core.Abstractions;

/// <summary>Persistence for LLM usage telemetry events.</summary>
public interface ILlmUsageStore
{
    Task RecordAsync(LlmUsageEvent usage, CancellationToken ct = default);
    Task<LlmUsageSummary> GetSummaryAsync(CancellationToken ct = default);
}

public sealed record LlmUsageSummary(
    int TotalCalls,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    long TotalLatencyMs,
    long LatencyP50Ms,
    long LatencyP95Ms,
    decimal TotalEstimatedCostUsd,
    IReadOnlyList<LlmUsageByOperation> ByOperation);

public sealed record LlmUsageByOperation(
    string Operation,
    int Calls,
    int PromptTokens,
    int CompletionTokens,
    long LatencyMs,
    long LatencyP50Ms,
    long LatencyP95Ms,
    decimal EstimatedCostUsd);
