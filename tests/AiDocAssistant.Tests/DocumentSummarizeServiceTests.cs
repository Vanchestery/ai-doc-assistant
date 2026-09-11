using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class DocumentSummarizeServiceTests
{
    private static readonly Guid DocId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private const string SampleJson =
        """
        {
          "doc_type": "счет",
          "number": "2026-041",
          "date": "2026-07-15",
          "counterparty": { "name": "ООО КофеPoint", "inn": "7701234567" },
          "items": [ { "name": "товар", "quantity": 1, "amount": 112700.0 } ],
          "total_amount": 112700.0,
          "currency": "RUB",
          "confidence": 0.9
        }
        """;

    [Fact]
    public async Task Summarize_calls_llm_and_returns_summary()
    {
        var llm = new FakeLlm("Сводка: 1 счёт на 112700 RUB от ООО КофеPoint.");
        var service = new DocumentSummarizeService(llm);

        var outcome = await service.SummarizeAsync(
        [
            new DocumentSummaryInput(DocId, "schet.pdf", SampleJson)
        ]);

        Assert.Contains("112700", outcome.Summary);
        Assert.Equal(1, llm.Calls);
        Assert.Single(outcome.Documents);
        Assert.Equal("2026-041", outcome.Documents[0].Number);
    }

    [Fact]
    public async Task Empty_documents_throw()
    {
        var service = new DocumentSummarizeService(new FakeLlm("x"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SummarizeAsync([]));
    }

    private sealed class FakeLlm : ILlmProvider
    {
        private readonly string _response;
        public int Calls { get; private set; }

        public FakeLlm(string response) => _response = response;

        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new LlmCompletion(_response, "fake-model", 50, 20));
        }
    }
}
