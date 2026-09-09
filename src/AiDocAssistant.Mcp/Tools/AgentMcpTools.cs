using System.ComponentModel;
using System.Text.Json;
using AiDocAssistant.Core.Agent;
using AiDocAssistant.Core.Entities;
using AiDocAssistant.Core.Services;
using ModelContextProtocol.Server;

namespace AiDocAssistant.Mcp.Tools;

/// <summary>MCP tools for explicit agent runs and goal routing.</summary>
[McpServerToolType]
public sealed class AgentMcpTools
{
    [McpServerTool, Description("List available agent tools (reconcile, summarize, generate_report).")]
    public static string ListAgentTools(AgentToolRegistry registry) =>
        JsonSerializer.Serialize(registry.Names.OrderBy(n => n).ToList(), McpJson.Options);

    [McpServerTool, Description("Reconcile two or more extracted documents and return mismatches.")]
    public static async Task<string> Reconcile(
        AgentTaskService tasks,
        [Description("Document GUIDs to reconcile (at least 2, status Extracted).")] Guid[] documentIds,
        CancellationToken cancellationToken) =>
        await RunTaskAsync(tasks, AgentToolNames.Reconcile, documentIds, cancellationToken);

    [McpServerTool, Description("Summarize extracted documents with LLM.")]
    public static async Task<string> Summarize(
        AgentTaskService tasks,
        [Description("Document GUIDs to summarize.")] Guid[] documentIds,
        CancellationToken cancellationToken) =>
        await RunTaskAsync(tasks, AgentToolNames.Summarize, documentIds, cancellationToken);

    [McpServerTool, Description("Generate an Excel report for extracted documents.")]
    public static async Task<string> GenerateReport(
        AgentTaskService tasks,
        [Description("Document GUIDs for the report.")] Guid[] documentIds,
        CancellationToken cancellationToken) =>
        await RunTaskAsync(tasks, AgentToolNames.GenerateReport, documentIds, cancellationToken);

    [McpServerTool, Description("Goal-mode: LLM picks the best tool for the goal, then runs it.")]
    public static async Task<string> RunAgentGoal(
        AgentGoalService goals,
        [Description("Natural language goal, e.g. 'сверь счета и покажи расхождения'.")] string goal,
        [Description("Document GUIDs to process.")] Guid[] documentIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return JsonSerializer.Serialize(new { error = "Goal is required." }, McpJson.Options);

        if (documentIds is null || documentIds.Length == 0)
            return JsonSerializer.Serialize(new { error = "At least one documentId is required." }, McpJson.Options);

        try
        {
            var outcome = await goals.RunAsync(goal, documentIds, cancellationToken);
            return JsonSerializer.Serialize(new
            {
                outcome.Goal,
                selectedTool = outcome.Routing.Tool,
                routingReason = outcome.Routing.Reasoning,
                routingModel = outcome.Routing.Model,
                task = ToTaskPayload(outcome.Action)
            }, McpJson.Options);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }

    [McpServerTool, Description("Get agent task status and result JSON by task id.")]
    public static async Task<string> GetAgentTask(
        AgentTaskService tasks,
        [Description("Agent task GUID from reconcile/summarize/generate_report/run_agent_goal.")] Guid taskId,
        CancellationToken cancellationToken)
    {
        var action = await tasks.GetAsync(taskId, cancellationToken);
        if (action is null)
            return JsonSerializer.Serialize(new { error = $"Task {taskId} not found." }, McpJson.Options);

        return JsonSerializer.Serialize(ToTaskPayload(action), McpJson.Options);
    }

    private static async Task<string> RunTaskAsync(
        AgentTaskService tasks,
        string tool,
        Guid[] documentIds,
        CancellationToken cancellationToken)
    {
        if (documentIds is null || documentIds.Length == 0)
            return JsonSerializer.Serialize(new { error = "At least one documentId is required." }, McpJson.Options);

        try
        {
            var action = await tasks.RunAsync(tool, documentIds, cancellationToken);
            return JsonSerializer.Serialize(ToTaskPayload(action), McpJson.Options);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }

    private static object ToTaskPayload(AgentAction action) =>
        new
        {
            action.Id,
            action.Tool,
            status = action.Status.ToString(),
            action.InputJson,
            action.ResultJson,
            action.Error,
            action.CreatedAt,
            action.CompletedAt
        };
}
