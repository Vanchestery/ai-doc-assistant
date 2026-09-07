using System.Text;
using System.Text.Json;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Llm;

namespace AiDocAssistant.Core.Services;

/// <summary>
/// Summary over a set of documents: compact fields from ExtractionResult → LLM → summary text.
/// </summary>
public sealed class DocumentSummarizeService
{
    private const string SystemPrompt =
        """
        Ты — ассистент бэк-офиса. По структурированным данным документов составь краткую сводку на русском.
        Укажи: сколько документов, типы, номера/даты, контрагентов, итоговые суммы и валюту.
        Если данных мало — скажи честно. Не выдумывай значения, которых нет во входе.
        """;

    private readonly ILlmProvider _llm;

    public DocumentSummarizeService(ILlmProvider llm) => _llm = llm;

    public async Task<SummarizeOutcome> SummarizeAsync(
        IReadOnlyList<DocumentSummaryInput> documents,
        CancellationToken ct = default)
    {
        if (documents.Count == 0)
            throw new ArgumentException("At least one document is required.", nameof(documents));

        var briefs = documents.Select(BuildBrief).ToList();
        var userPrompt = BuildUserPrompt(briefs);

        var completion = await _llm.CompleteAsync(
            new LlmRequest(
            [
                LlmMessage.System(SystemPrompt),
                LlmMessage.User(userPrompt)
            ],
            JsonMode: false,
            Temperature: 0.2,
            Operation: LlmOperations.Summarize),
            ct);

        return new SummarizeOutcome(
            completion.Content.Trim(),
            completion.Model,
            completion.PromptTokens,
            completion.CompletionTokens,
            briefs);
    }

    private static string BuildUserPrompt(IReadOnlyList<DocumentBrief> briefs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Данные документов:");
        sb.AppendLine();

        foreach (var b in briefs)
        {
            sb.AppendLine($"[{b.FileName}] id={b.DocumentId}");
            sb.AppendLine($"  doc_type: {b.DocType ?? "—"}");
            sb.AppendLine($"  number: {b.Number ?? "—"}");
            sb.AppendLine($"  date: {b.Date ?? "—"}");
            sb.AppendLine($"  counterparty: {b.CounterpartyName ?? "—"} (inn: {b.CounterpartyInn ?? "—"})");
            sb.AppendLine($"  total_amount: {b.TotalAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—"}");
            sb.AppendLine($"  currency: {b.Currency ?? "—"}");
            sb.AppendLine($"  items_count: {b.ItemsCount?.ToString() ?? "—"}");
            sb.AppendLine();
        }

        sb.AppendLine("Составь сводку по этим документам.");
        return sb.ToString();
    }

    private static DocumentBrief BuildBrief(DocumentSummaryInput input)
    {
        using var doc = JsonDocument.Parse(input.ExtractionJson);
        var root = doc.RootElement;

        return new DocumentBrief(
            input.DocumentId,
            input.FileName,
            ReadString(root, "doc_type"),
            ReadString(root, "number"),
            ReadString(root, "date"),
            ReadNestedString(root, "counterparty", "name"),
            ReadNestedString(root, "counterparty", "inn"),
            ReadDecimal(root, "total_amount"),
            ReadString(root, "currency"),
            ReadItemsCount(root));
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static string? ReadNestedString(JsonElement root, string obj, string name)
    {
        if (!root.TryGetProperty(obj, out var nested) || nested.ValueKind != JsonValueKind.Object)
            return null;

        return nested.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
    }

    private static decimal? ReadDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
            return null;

        return el.ValueKind == JsonValueKind.Number ? el.GetDecimal() : null;
    }

    private static int? ReadItemsCount(JsonElement root) =>
        root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.GetArrayLength()
            : null;
}

public sealed record DocumentSummaryInput(Guid DocumentId, string FileName, string ExtractionJson);

public sealed record DocumentBrief(
    Guid DocumentId,
    string FileName,
    string? DocType,
    string? Number,
    string? Date,
    string? CounterpartyName,
    string? CounterpartyInn,
    decimal? TotalAmount,
    string? Currency,
    int? ItemsCount);

public sealed record SummarizeOutcome(
    string Summary,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    IReadOnlyList<DocumentBrief> Documents);
