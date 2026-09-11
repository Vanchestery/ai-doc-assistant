using System.Net;
using System.Text;
using AiDocAssistant.Infrastructure.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiDocAssistant.Tests;

public class OpenAiCompatibleEmbeddingProviderTests
{
    [Fact]
    public async Task Parses_embeddings_from_openai_compatible_response()
    {
        const string json =
            """
            { "data": [
                { "index": 0, "embedding": [0.1, 0.2, 0.3] },
                { "index": 1, "embedding": [0.4, 0.5, 0.6] }
            ] }
            """;
        var provider = BuildProvider(json);

        var vectors = await provider.EmbedAsync(["первый", "второй"]);

        Assert.Equal(2, vectors.Count);
        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, vectors[0]);
        Assert.Equal(new[] { 0.4f, 0.5f, 0.6f }, vectors[1]);
    }

    [Fact]
    public async Task Orders_vectors_by_index()
    {
        const string json =
            """
            { "data": [
                { "index": 1, "embedding": [9.0] },
                { "index": 0, "embedding": [1.0] }
            ] }
            """;
        var provider = BuildProvider(json);

        var vectors = await provider.EmbedAsync(["a", "b"]);

        Assert.Equal(1.0f, vectors[0][0]); // index 0 идёт первым, несмотря на порядок в ответе
        Assert.Equal(9.0f, vectors[1][0]);
    }

    [Fact]
    public async Task Empty_input_returns_empty_without_http_call()
    {
        var handler = new StubHandler("{}") ;
        var provider = BuildProvider(handler);

        var vectors = await provider.EmbedAsync([]);

        Assert.Empty(vectors);
        Assert.Equal(0, handler.CallCount);
    }

    private static OpenAiCompatibleEmbeddingProvider BuildProvider(string responseJson) =>
        BuildProvider(new StubHandler(responseJson));

    private static OpenAiCompatibleEmbeddingProvider BuildProvider(StubHandler handler)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new EmbeddingOptions { BaseUrl = "http://localhost:11434", Dimension = 3 });
        return new OpenAiCompatibleEmbeddingProvider(
            http, options, NullLogger<OpenAiCompatibleEmbeddingProvider>.Instance);
    }

    private sealed class StubHandler(string responseJson) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
