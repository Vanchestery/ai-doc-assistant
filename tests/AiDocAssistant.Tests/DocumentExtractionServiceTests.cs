using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class DocumentExtractionServiceTests
{
    private const string ValidJson =
        """
        {
          "doc_type": "накладная",
          "number": "ТН-42",
          "date": "2026-03-15",
          "counterparty": { "name": "ООО Ромашка", "inn": "7701234567" },
          "items": [ { "name": "Бумага А4", "quantity": 10, "unit": "пач", "unit_price": 350.0, "amount": 3500.0 } ],
          "total_amount": 3500.0,
          "vat_amount": 583.33,
          "currency": "RUB",
          "confidence": 0.93
        }
        """;

    [Fact]
    public async Task Valid_llm_response_is_returned_with_confidence()
    {
        var llm = new FakeLlm(ValidJson);
        var service = new DocumentExtractionService(llm);

        var outcome = await service.ExtractAsync("Накладная ТН-42 от 15.03.2026 ...");

        Assert.Equal(0.93, outcome.Confidence);
        Assert.Equal(1, llm.Calls);
        Assert.Contains("ТН-42", outcome.Json);
    }

    [Fact]
    public async Task Invalid_json_triggers_one_retry()
    {
        var llm = new FakeLlm("это не json", ValidJson);
        var service = new DocumentExtractionService(llm);

        var outcome = await service.ExtractAsync("текст");

        Assert.Equal(2, llm.Calls);
        Assert.Equal(0.93, outcome.Confidence);
    }

    [Fact]
    public async Task Missing_required_fields_trigger_retry_then_fail()
    {
        var llm = new FakeLlm("""{"doc_type":"акт"}""", """{"doc_type":"акт"}""");
        var service = new DocumentExtractionService(llm);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExtractAsync("текст"));
        Assert.Equal(2, llm.Calls);
    }

    [Fact]
    public async Task Token_usage_is_summed_across_retry()
    {
        var llm = new FakeLlm("не json", ValidJson);
        var service = new DocumentExtractionService(llm);

        var outcome = await service.ExtractAsync("текст");

        // FakeLlm отдаёт 100/50 за вызов; retry должен суммировать
        Assert.Equal(200, outcome.PromptTokens);
        Assert.Equal(100, outcome.CompletionTokens);
    }

    private sealed class FakeLlm : ILlmProvider
    {
        private readonly Queue<string> _responses;
        public int Calls { get; private set; }

        public FakeLlm(params string[] responses) => _responses = new Queue<string>(responses);

        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        {
            Calls++;
            var content = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
            return Task.FromResult(new LlmCompletion(content, "fake-model", 100, 50));
        }
    }
}
