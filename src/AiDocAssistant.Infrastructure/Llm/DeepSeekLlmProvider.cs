using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiDocAssistant.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiDocAssistant.Infrastructure.Llm;

/// <summary>
/// ILlmProvider over DeepSeek's OpenAI-compatible API.
/// Thin HttpClient, no SDK — full control over the request and telemetry
/// (tokens/latency are needed for Phase 4 metrics). See DECISIONS.md.
/// </summary>
public class DeepSeekLlmProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly DeepSeekOptions _options;
    private readonly ILogger<DeepSeekLlmProvider> _logger;

    public DeepSeekLlmProvider(
        HttpClient http,
        IOptions<DeepSeekOptions> options,
        ILogger<DeepSeekLlmProvider> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException(
                "DeepSeek:ApiKey is not set. Set it via user-secrets or the DeepSeek__ApiKey environment variable.");

        _http = http;
        _http.BaseAddress = new Uri(_options.BaseUrl);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _http.Timeout = TimeSpan.FromMinutes(3);
    }

    public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var body = new ChatCompletionRequest
        {
            Model = _options.Model,
            Messages = request.Messages
                .Select(m => new ChatMessage { Role = m.Role, Content = m.Content })
                .ToList(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            ResponseFormat = request.JsonMode ? new ResponseFormat { Type = "json_object" } : null
        };

        var started = System.Diagnostics.Stopwatch.StartNew();
        using var response = await _http.PostAsJsonAsync("/chat/completions", body, JsonOpts, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"DeepSeek returned {(int)response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Empty DeepSeek response");

        var content = result.Choices.FirstOrDefault()?.Message?.Content
            ?? throw new InvalidOperationException("DeepSeek response has no choices[0].message.content");

        _logger.LogInformation(
            "LLM call: model={Model} promptTokens={Prompt} completionTokens={Completion} latencyMs={Latency}",
            result.Model, result.Usage?.PromptTokens, result.Usage?.CompletionTokens,
            started.ElapsedMilliseconds);

        return new LlmCompletion(
            content,
            result.Model ?? _options.Model,
            result.Usage?.PromptTokens ?? 0,
            result.Usage?.CompletionTokens ?? 0);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // --- OpenAI-compatible protocol DTOs ---

    private sealed class ChatCompletionRequest
    {
        public string Model { get; set; } = "";
        public List<ChatMessage> Messages { get; set; } = [];
        public double Temperature { get; set; }
        [JsonPropertyName("max_tokens")] public int? MaxTokens { get; set; }
        [JsonPropertyName("response_format")] public ResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class ChatMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    private sealed class ResponseFormat
    {
        public string Type { get; set; } = "";
    }

    private sealed class ChatCompletionResponse
    {
        public string? Model { get; set; }
        public List<Choice> Choices { get; set; } = [];
        public Usage? Usage { get; set; }
    }

    private sealed class Choice
    {
        public ChatMessage? Message { get; set; }
    }

    private sealed class Usage
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
    }
}
