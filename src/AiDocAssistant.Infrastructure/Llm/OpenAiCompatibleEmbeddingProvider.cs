using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiDocAssistant.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiDocAssistant.Infrastructure.Llm;

/// <summary>
/// IEmbeddingProvider over an OpenAI-compatible /v1/embeddings endpoint.
/// Same thin-HttpClient approach as DeepSeekLlmProvider — works with Ollama
/// (local) and any OpenAI-compatible provider. See DECISIONS.md #13.
/// </summary>
public sealed class OpenAiCompatibleEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _http;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<OpenAiCompatibleEmbeddingProvider> _logger;

    public OpenAiCompatibleEmbeddingProvider(
        HttpClient http,
        IOptions<EmbeddingOptions> options,
        ILogger<OpenAiCompatibleEmbeddingProvider> logger)
    {
        _options = options.Value;
        _logger = logger;

        _http = http;
        _http.BaseAddress = new Uri(_options.BaseUrl);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _http.Timeout = TimeSpan.FromMinutes(3);
    }

    public int Dimension => _options.Dimension;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0)
            return [];

        var body = new EmbeddingRequest { Model = _options.Model, Input = texts };

        using var response = await _http.PostAsJsonAsync("/v1/embeddings", body, JsonOpts, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Embedder returned {(int)response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Empty embedder response.");

        // Keep the input order via the index field, just in case.
        var vectors = result.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToArray();

        if (vectors.Length > 0 && vectors[0].Length != _options.Dimension)
            _logger.LogWarning(
                "Embedding dimension {Actual} does not match the configured {Expected} — check the model and the vector(N) schema.",
                vectors[0].Length, _options.Dimension);

        return vectors;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class EmbeddingRequest
    {
        public string Model { get; set; } = "";
        public IReadOnlyList<string> Input { get; set; } = [];
    }

    private sealed class EmbeddingResponse
    {
        public List<EmbeddingData> Data { get; set; } = [];
    }

    private sealed class EmbeddingData
    {
        public int Index { get; set; }
        public float[] Embedding { get; set; } = [];
    }
}
