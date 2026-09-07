using System.Net;
using System.Net.Http.Json;
using ai_doc_assistant.Controllers;
using Microsoft.AspNetCore.Components;

namespace ai_doc_assistant.Services;

public sealed class AgentApiClient(IHttpClientFactory httpFactory, NavigationManager navigation)
{
    public async Task<IReadOnlyList<string>> GetToolsAsync(CancellationToken ct = default)
    {
        using var http = CreateClient();
        return await http.GetFromJsonAsync<IReadOnlyList<string>>("api/agent/tools", ct)
               ?? [];
    }

    public async Task<AgentTaskDto> RunTaskAsync(
        string tool,
        IReadOnlyList<Guid> documentIds,
        CancellationToken ct = default)
    {
        using var http = CreateClient();
        var response = await http.PostAsJsonAsync(
            "api/agent/tasks",
            new RunAgentTaskRequest(tool, documentIds),
            ct);

        return await ReadTaskResponseAsync(response, ct);
    }

    public async Task<(AgentGoalDto Goal, AgentTaskDto Task)> RunGoalAsync(
        string goal,
        IReadOnlyList<Guid> documentIds,
        CancellationToken ct = default)
    {
        using var http = CreateClient();
        var response = await http.PostAsJsonAsync(
            "api/agent/goals",
            new RunAgentGoalRequest(goal, documentIds),
            ct);

        var dto = await response.Content.ReadFromJsonAsync<AgentGoalDto>(cancellationToken: ct);
        if (dto is null)
            throw new InvalidOperationException(await ReadErrorAsync(response, ct));

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.UnprocessableEntity)
            throw new InvalidOperationException(await ReadErrorAsync(response, ct));

        return (dto, dto.Task);
    }

    public async Task<AgentTaskDto?> GetTaskAsync(Guid id, CancellationToken ct = default)
    {
        using var http = CreateClient();
        var response = await http.GetAsync($"api/agent/tasks/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentTaskDto>(cancellationToken: ct);
    }

    public string GetReportUrl(Guid taskId) =>
        new Uri(new Uri(navigation.BaseUri), $"api/agent/tasks/{taskId}/report").ToString();

    private static async Task<AgentTaskDto> ReadTaskResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var dto = await response.Content.ReadFromJsonAsync<AgentTaskDto>(cancellationToken: ct);
        if (dto is null)
            throw new InvalidOperationException(await ReadErrorAsync(response, ct));

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.UnprocessableEntity)
            throw new InvalidOperationException(await ReadErrorAsync(response, ct));

        return dto;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(body)
            ? response.ReasonPhrase ?? "Request failed"
            : body.Trim('"');
    }

    private HttpClient CreateClient()
    {
        var client = httpFactory.CreateClient();
        client.BaseAddress = new Uri(navigation.BaseUri);
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    }
}
