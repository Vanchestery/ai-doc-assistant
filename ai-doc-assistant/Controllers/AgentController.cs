using System.Text.Json;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Agent;
using AiDocAssistant.Core.Entities;
using AiDocAssistant.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ai_doc_assistant.Controllers;

[ApiController]
[Route("api/agent")]
[IgnoreAntiforgeryToken]
public class AgentController : ControllerBase
{
    private readonly AgentTaskService _tasks;
    private readonly AgentGoalService _goals;
    private readonly AgentToolRegistry _tools;
    private readonly IFileStorage _storage;

    public AgentController(
        AgentTaskService tasks,
        AgentGoalService goals,
        AgentToolRegistry tools,
        IFileStorage storage)
    {
        _tasks = tasks;
        _goals = goals;
        _tools = tools;
        _storage = storage;
    }

    /// <summary>Available tools (explicit Phase 3 mode).</summary>
    [HttpGet("tools")]
    public ActionResult<IReadOnlyList<string>> ListTools() =>
        _tools.Names.OrderBy(n => n).ToList();

    /// <summary>Run a tool over documents.</summary>
    [HttpPost("tasks")]
    [ProducesResponseType(typeof(AgentTaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(AgentTaskDto), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentTaskDto>> RunTask(
        [FromBody] RunAgentTaskRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Tool))
            return BadRequest("Specify a tool (reconcile, summarize, generate_report).");

        if (request.DocumentIds is null || request.DocumentIds.Count == 0)
            return BadRequest("Specify documentIds.");

        try
        {
            var action = await _tasks.RunAsync(request.Tool, request.DocumentIds, ct);
            var dto = AgentTaskDto.FromEntity(action);
            return action.Status == AgentActionStatus.Failed
                ? UnprocessableEntity(dto)
                : CreatedAtAction(nameof(GetTask), new { id = action.Id }, dto);
        }
        catch (KeyNotFoundException e)
        {
            return BadRequest(e.Message);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>Goal mode: the LLM picks a tool, then the task runs.</summary>
    [HttpPost("goals")]
    [ProducesResponseType(typeof(AgentGoalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(AgentGoalDto), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentGoalDto>> RunGoal(
        [FromBody] RunAgentGoalRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Goal))
            return BadRequest("Specify a goal — what to do with the documents.");

        if (request.DocumentIds is null || request.DocumentIds.Count == 0)
            return BadRequest("Specify documentIds.");

        try
        {
            var outcome = await _goals.RunAsync(request.Goal, request.DocumentIds, ct);
            var dto = AgentGoalDto.FromOutcome(outcome);
            return outcome.Action.Status == AgentActionStatus.Failed
                ? UnprocessableEntity(dto)
                : CreatedAtAction(nameof(GetTask), new { id = outcome.Action.Id }, dto);
        }
        catch (KeyNotFoundException e)
        {
            return BadRequest(e.Message);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpGet("tasks/{id:guid}")]
    [ProducesResponseType(typeof(AgentTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentTaskDto>> GetTask(Guid id, CancellationToken ct)
    {
        var action = await _tasks.GetAsync(id, ct);
        return action is null ? NotFound() : AgentTaskDto.FromEntity(action);
    }

    /// <summary>Download the xlsx report for a generate_report task.</summary>
    [HttpGet("tasks/{id:guid}/report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadReport(Guid id, CancellationToken ct)
    {
        var action = await _tasks.GetAsync(id, ct);
        if (action is null
            || action.Tool != AgentToolNames.GenerateReport
            || action.Status != AgentActionStatus.Completed
            || string.IsNullOrWhiteSpace(action.ResultJson))
        {
            return NotFound();
        }

        using var doc = JsonDocument.Parse(action.ResultJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("storagePath", out var pathEl)
            || !root.TryGetProperty("fileName", out var nameEl))
        {
            return NotFound();
        }

        var storagePath = pathEl.GetString();
        if (string.IsNullOrWhiteSpace(storagePath))
            return NotFound();

        var fullPath = _storage.GetFullPath(storagePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var fileName = nameEl.GetString() ?? "report.xlsx";
        return PhysicalFile(
            fullPath,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}

public record RunAgentTaskRequest(string Tool, IReadOnlyList<Guid> DocumentIds);

public record RunAgentGoalRequest(string Goal, IReadOnlyList<Guid> DocumentIds);

public record AgentGoalDto(
    string Goal,
    string SelectedTool,
    string RoutingReason,
    string RoutingModel,
    int RoutingPromptTokens,
    int RoutingCompletionTokens,
    AgentTaskDto Task)
{
    public static AgentGoalDto FromOutcome(AgentGoalOutcome outcome) =>
        new(
            outcome.Goal,
            outcome.Routing.Tool,
            outcome.Routing.Reasoning,
            outcome.Routing.Model,
            outcome.Routing.PromptTokens,
            outcome.Routing.CompletionTokens,
            AgentTaskDto.FromEntity(outcome.Action));
}

public record AgentTaskDto(
    Guid Id,
    string Tool,
    string Status,
    string InputJson,
    string? ResultJson,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static AgentTaskDto FromEntity(AgentAction action) =>
        new(
            action.Id,
            action.Tool,
            action.Status.ToString(),
            action.InputJson,
            action.ResultJson,
            action.Error,
            action.CreatedAt,
            action.CompletedAt);
}
