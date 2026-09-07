using System.Globalization;
using System.Text.Json;

namespace AiDocAssistant.Core.Services;

/// <summary>
/// Compare structured extraction across documents — pure logic, no LLM and no database.
/// Data source: JSON from ExtractionResult (Phase 1).
/// </summary>
public sealed class DocumentReconcileService
{
    private static readonly string[] ComparedFields =
    [
        "number",
        "date",
        "total_amount",
        "vat_amount",
        "currency",
        "counterparty.name",
        "counterparty.inn"
    ];

    public ReconcileOutcome Reconcile(IReadOnlyList<DocumentExtractionSnapshot> documents)
    {
        if (documents.Count < 2)
            throw new ArgumentException("Reconcile needs at least 2 documents.", nameof(documents));

        var summaries = documents
            .Select(d => new DocumentReconcileSummary(
                d.DocumentId,
                d.FileName,
                ExtractFieldValues(d.ExtractionJson)))
            .ToList();

        var discrepancies = new List<FieldDiscrepancy>();

        foreach (var field in ComparedFields)
        {
            var values = summaries
                .Select(s => new DocumentFieldValue(
                    s.DocumentId,
                    s.FileName,
                    s.Fields.GetValueOrDefault(field)))
                .ToList();

            if (AllSame(values.Select(v => v.Value)))
                continue;

            discrepancies.Add(new FieldDiscrepancy(
                field,
                values,
                BuildDescription(field, values)));
        }

        var itemsMismatch = CompareItemsCount(summaries);
        if (itemsMismatch is not null)
            discrepancies.Add(itemsMismatch);

        var hasIssues = discrepancies.Count > 0;
        var summary = hasIssues
            ? $"Найдено расхождений: {discrepancies.Count}."
            : "Расхождений по ключевым полям не обнаружено.";

        return new ReconcileOutcome(summaries, discrepancies, hasIssues, summary);
    }

    private static FieldDiscrepancy? CompareItemsCount(IReadOnlyList<DocumentReconcileSummary> summaries)
    {
        var counts = summaries
            .Select(s => new DocumentFieldValue(
                s.DocumentId,
                s.FileName,
                s.Fields.GetValueOrDefault("items.count")))
            .ToList();

        if (AllSame(counts.Select(c => c.Value)))
            return null;

        return new FieldDiscrepancy(
            "items.count",
            counts,
            BuildDescription("items.count", counts));
    }

    private static Dictionary<string, string?> ExtractFieldValues(string extractionJson)
    {
        using var doc = JsonDocument.Parse(extractionJson);
        var root = doc.RootElement;

        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in ComparedFields)
        {
            fields[field] = field switch
            {
                "counterparty.name" => ReadNestedString(root, "counterparty", "name"),
                "counterparty.inn" => ReadNestedString(root, "counterparty", "inn"),
                "total_amount" or "vat_amount" => ReadDecimalString(root, field),
                _ => ReadScalarString(root, field)
            };
        }

        fields["items.count"] = root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.GetArrayLength().ToString(CultureInfo.InvariantCulture)
            : null;

        return fields;
    }

    private static string? ReadScalarString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind != JsonValueKind.Null
            ? el.ToString()
            : null;

    private static string? ReadNestedString(JsonElement root, string obj, string name)
    {
        if (!root.TryGetProperty(obj, out var nested) || nested.ValueKind != JsonValueKind.Object)
            return null;

        return nested.TryGetProperty(name, out var el) && el.ValueKind != JsonValueKind.Null
            ? el.ToString()
            : null;
    }

    private static string? ReadDecimalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
            return null;

        return el.ValueKind == JsonValueKind.Number
            ? el.GetDecimal().ToString(CultureInfo.InvariantCulture)
            : el.ToString();
    }

    private static bool AllSame(IEnumerable<string?> values)
    {
        var normalized = values
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count <= 1;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildDescription(string field, IReadOnlyList<DocumentFieldValue> values)
    {
        var parts = values
            .Select(v => $"{v.FileName}: {v.Value ?? "null"}")
            .ToArray();

        return $"Поле «{field}» различается — {string.Join("; ", parts)}.";
    }
}

public sealed record DocumentExtractionSnapshot(Guid DocumentId, string FileName, string ExtractionJson);

public sealed record DocumentReconcileSummary(
    Guid DocumentId,
    string FileName,
    IReadOnlyDictionary<string, string?> Fields);

public sealed record DocumentFieldValue(Guid DocumentId, string FileName, string? Value);

public sealed record FieldDiscrepancy(
    string Field,
    IReadOnlyList<DocumentFieldValue> Values,
    string Description);

public sealed record ReconcileOutcome(
    IReadOnlyList<DocumentReconcileSummary> Documents,
    IReadOnlyList<FieldDiscrepancy> Discrepancies,
    bool HasDiscrepancies,
    string Summary);
