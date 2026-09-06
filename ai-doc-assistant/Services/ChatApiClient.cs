using System.Net;
using System.Net.Http.Json;
using ai_doc_assistant.Controllers;
using Microsoft.AspNetCore.Components;

namespace ai_doc_assistant.Services;

public sealed class ChatApiClient(IHttpClientFactory httpFactory, NavigationManager navigation)
{
    public async Task<CreateSessionDto> CreateSessionAsync(CancellationToken ct = default)
    {
        using var http = CreateClient();
        var response = await http.PostAsync("api/chat/sessions", null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateSessionDto>(cancellationToken: ct))!;
    }

    public async Task<ChatSessionDto?> GetSessionAsync(Guid id, CancellationToken ct = default)
    {
        using var http = CreateClient();
        var response = await http.GetAsync($"api/chat/sessions/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatSessionDto>(cancellationToken: ct);
    }

    public async Task<AskResponseDto> AskAsync(
        Guid sessionId,
        string question,
        Guid? documentId,
        CancellationToken ct = default)
    {
        using var http = CreateClient();
        var response = await http.PostAsJsonAsync(
            $"api/chat/sessions/{sessionId}/messages",
            new AskRequestDto(question, documentId),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Request failed"
                : body.Trim('"'));
        }

        return (await response.Content.ReadFromJsonAsync<AskResponseDto>(cancellationToken: ct))!;
    }

    private HttpClient CreateClient()
    {
        var client = httpFactory.CreateClient();
        client.BaseAddress = new Uri(navigation.BaseUri);
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    }
}
