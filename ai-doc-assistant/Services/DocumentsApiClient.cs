using ai_doc_assistant.Controllers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace ai_doc_assistant.Services;

public sealed class DocumentsApiClient(IHttpClientFactory httpFactory, NavigationManager navigation)
{
    public async Task<IReadOnlyList<DocumentListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        using var http = CreateClient();
        return await http.GetFromJsonAsync<IReadOnlyList<DocumentListItemDto>>("api/documents", ct)
               ?? Array.Empty<DocumentListItemDto>();
    }

    public async Task<DocumentDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var http = CreateClient();
        var response = await http.GetAsync($"api/documents/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DocumentDetailDto>(cancellationToken: ct);
    }

    public async Task<DocumentDetailDto> UploadAsync(IBrowserFile file, CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var content = new MultipartFormDataContent();
        await using var browserStream = file.OpenReadStream(maxAllowedSize: 50_000_000, ct);
        using var buffer = new MemoryStream();
        await browserStream.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        content.Add(new StreamContent(buffer), "file", file.Name);

        var response = await http.PostAsync("api/documents", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Upload failed ({(int)response.StatusCode}): {body}");
        }

        return (await response.Content.ReadFromJsonAsync<DocumentDetailDto>(cancellationToken: ct))!;
    }

    private HttpClient CreateClient()
    {
        var client = httpFactory.CreateClient();
        client.BaseAddress = new Uri(navigation.BaseUri);
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    }
}
