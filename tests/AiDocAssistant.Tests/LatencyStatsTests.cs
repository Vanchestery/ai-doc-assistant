using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class LatencyStatsTests
{
    [Fact]
    public void Percentile_p50_and_p95()
    {
        var values = new long[] { 100, 200, 300, 400, 1000 };

        Assert.Equal(300, LatencyStats.Percentile(values, 50));
        Assert.Equal(1000, LatencyStats.Percentile(values, 95));
    }

    [Fact]
    public void Empty_returns_zero()
    {
        Assert.Equal(0, LatencyStats.Percentile(Array.Empty<long>(), 50));
    }
}
