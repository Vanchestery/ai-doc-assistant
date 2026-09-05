using System.Diagnostics;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Entities;
using AiDocAssistant.Core.Llm;
using AiDocAssistant.Core.Services;

namespace AiDocAssistant.Infrastructure.Llm;

/// <summary>ILlmProvider decorator: writes telemetry for each call to the database.</summary>
public sealed class MeteringLlmProvider : ILlmProvider
{
    private readonly ILlmProvider _inner;
    private readonly ILlmUsageStore _usage;
    private readonly LlmCostEstimator _cost;

    public MeteringLlmProvider(ILlmProvider inner, ILlmUsageStore usage, LlmCostEstimator cost)
    {
        _inner = inner;
        _usage = usage;
        _cost = cost;
    }

    public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var completion = await _inner.CompleteAsync(request, ct);
        sw.Stop();

        var operation = string.IsNullOrWhiteSpace(request.Operation)
            ? LlmOperations.Unknown
            : request.Operation.Trim();

        await _usage.RecordAsync(
            new LlmUsageEvent
            {
                Id = Guid.NewGuid(),
                Operation = operation,
                Model = completion.Model,
                PromptTokens = completion.PromptTokens,
                CompletionTokens = completion.CompletionTokens,
                LatencyMs = sw.ElapsedMilliseconds,
                EstimatedCostUsd = _cost.Estimate(completion.PromptTokens, completion.CompletionTokens),
                CreatedAt = DateTimeOffset.UtcNow
            },
            ct);

        return completion;
    }
}
