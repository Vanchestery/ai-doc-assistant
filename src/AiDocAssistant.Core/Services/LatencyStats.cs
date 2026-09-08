namespace AiDocAssistant.Core.Services;

public static class LatencyStats
{
    public static long Percentile(IReadOnlyList<long> values, int percentile)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(v => v).ToList();
        var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        rank = Math.Clamp(rank, 0, sorted.Count - 1);
        return sorted[rank];
    }
}
