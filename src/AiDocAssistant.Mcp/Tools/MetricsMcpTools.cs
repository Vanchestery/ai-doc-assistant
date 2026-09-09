using System.ComponentModel;
using System.Text.Json;
using AiDocAssistant.Core.Services;
using ModelContextProtocol.Server;

namespace AiDocAssistant.Mcp.Tools;

/// <summary>MCP tool for the same snapshot as GET /api/metrics/summary.</summary>
[McpServerToolType]
public sealed class MetricsMcpTools
{
    [McpServerTool, Description("LLM usage, DB counts, and eval suite summary (same as GET /api/metrics/summary).")]
    public static async Task<string> GetMetricsSummary(
        MetricsService metrics,
        CancellationToken cancellationToken)
    {
        var summary = await metrics.GetSummaryAsync(cancellationToken);
        return JsonSerializer.Serialize(summary, McpJson.Options);
    }
}
