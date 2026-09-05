using System.Text.Json;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Llm;

namespace AiDocAssistant.Core.Services;

/// <summary>
/// Structured extraction: document text -> strict JSON with fields.
/// Pattern: JSON mode + schema in the prompt + validation + 1 retry (see DECISIONS.md).
/// </summary>
public class DocumentExtractionService
{
    private readonly ILlmProvider _llm;

    // Context cap — cost control (Phase 4 will measure the effect more precisely)
    private const int MaxInputChars = 15_000;

    private const string SystemPrompt =
        """
        Ты — система извлечения структурированных данных из документов бэк-офиса
        (накладные, счета, акты, отчёты). Извлеки данные из текста документа и верни
        СТРОГО один json-объект по схеме ниже. Никакого текста вне json.

        Схема (все поля обязательны; если значения нет в документе — null):
        {
          "doc_type": "накладная | счет | акт | отчет | другое",
          "number": "номер документа или null",
          "date": "дата документа в формате YYYY-MM-DD или null",
          "counterparty": { "name": "контрагент или null", "inn": "ИНН или null" },
          "items": [
            { "name": "наименование", "quantity": число или null,
              "unit": "ед. изм. или null", "unit_price": число или null,
              "amount": число или null }
          ],
          "total_amount": итоговая сумма числом или null,
          "vat_amount": сумма НДС числом или null,
          "currency": "RUB | USD | ... или null",
          "confidence": число от 0 до 1 — твоя уверенность в извлечении
        }

        Правила: числа — без разделителей тысяч, точка как десятичный разделитель;
        не выдумывай значения, которых нет в тексте; items может быть пустым массивом.
        """;

    private static readonly string[] RequiredFields =
        ["doc_type", "number", "date", "counterparty", "items", "total_amount", "currency", "confidence"];

    public DocumentExtractionService(ILlmProvider llm) => _llm = llm;

    public async Task<ExtractionOutcome> ExtractAsync(string documentText, CancellationToken ct = default)
    {
        var text = documentText.Length > MaxInputChars
            ? documentText[..MaxInputChars]
            : documentText;

        var messages = new List<LlmMessage>
        {
            LlmMessage.System(SystemPrompt),
            LlmMessage.User($"Текст документа:\n\n{text}")
        };

        var completion = await _llm.CompleteAsync(
            new LlmRequest(messages, JsonMode: true, Temperature: 0.1, Operation: LlmOperations.Extraction), ct);

        var (json, error) = TryValidate(completion.Content);
        var totalPrompt = completion.PromptTokens;
        var totalCompletion = completion.CompletionTokens;

        if (error is not null)
        {
            // One retry: send the model its reply and the validation error
            messages.Add(LlmMessage.Assistant(completion.Content));
            messages.Add(LlmMessage.User(
                $"Твой json не прошёл валидацию: {error}. Верни исправленный json-объект строго по схеме."));

            completion = await _llm.CompleteAsync(
                new LlmRequest(messages, JsonMode: true, Temperature: 0.1, Operation: LlmOperations.Extraction), ct);

            totalPrompt += completion.PromptTokens;
            totalCompletion += completion.CompletionTokens;

            (json, error) = TryValidate(completion.Content);
            if (error is not null)
                throw new InvalidOperationException($"LLM returned an invalid result after retry: {error}");
        }

        return new ExtractionOutcome(
            Json: json!,
            Confidence: ReadConfidence(json!),
            Model: completion.Model,
            PromptTokens: totalPrompt,
            CompletionTokens: totalCompletion);
    }

    /// <summary>Check: a valid JSON object with every required field.</summary>
    private static (string? Json, string? Error) TryValidate(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return (null, "корень не является json-объектом");

            var missing = RequiredFields
                .Where(f => !doc.RootElement.TryGetProperty(f, out _))
                .ToArray();

            return missing.Length > 0
                ? (null, $"отсутствуют поля: {string.Join(", ", missing)}")
                : (content, null);
        }
        catch (JsonException e)
        {
            return (null, $"невалидный json: {e.Message}");
        }
    }

    private static double? ReadConfidence(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number
            ? c.GetDouble()
            : null;
    }
}

public sealed record ExtractionOutcome(
    string Json,
    double? Confidence,
    string Model,
    int PromptTokens,
    int CompletionTokens);
