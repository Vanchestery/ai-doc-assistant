namespace AiDocAssistant.Core.Abstractions;

/// <summary>Aggregate row counts for the metrics dashboard.</summary>
public interface IDataCountsProvider
{
    Task<DataCountsSnapshot> GetCountsAsync(CancellationToken ct = default);
}

public sealed record DataCountsSnapshot(
    int Documents,
    int ExtractedDocuments,
    int ChatSessions,
    int AgentTasks,
    int LlmUsageEvents);
