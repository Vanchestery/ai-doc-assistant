using System.Net.Http.Json;
using ai_doc_assistant.Controllers;
using Microsoft.AspNetCore.Components;

namespace ai_doc_assistant.Services;

public sealed class MetricsApiClient(IHttpClientFactory httpFactory, NavigationManager navigation)
{
    public async Task<MetricsSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        using var http = CreateClient();
        return (await http.GetFromJsonAsync<MetricsSummaryDto>("api/metrics/summary", ct))!;
    }

    public async Task<EvalSuiteDto> GetEvalsAsync(CancellationToken ct = default)
    {
        using var http = CreateClient();
        return (await http.GetFromJsonAsync<EvalSuiteDto>("api/metrics/evals", ct))!;
    }

    private HttpClient CreateClient()
    {
        var client = httpFactory.CreateClient();
        client.BaseAddress = new Uri(navigation.BaseUri);
        return client;
    }
}
