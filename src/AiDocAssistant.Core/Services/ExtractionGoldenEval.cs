using System.Globalization;
using System.Text.Json;

namespace AiDocAssistant.Core.Services;

/// <summary>
/// Compare actual extraction JSON with a golden expected file — offline eval, no LLM.
/// </summary>
public static class ExtractionGoldenEval
{
    private static readonly string[] ComparedFields =
    [
        "doc_type",
        "number",
        "date",
        "total_amount",
        "currency",
        "counterparty.name",
        "counterparty.inn"
    ];

    public static GoldenEvalOutcome Compare(string caseName, string expectedJson, string actualJson)
    {
        var mismatches = new List<string>();
        var compared = 0;

        foreach (var field in ComparedFields)
        {
            var expected = GetField(expectedJson, field);
            var actual = GetField(actualJson, field);

            if (expected is null && actual is null)
                continue;

            compared++;
            if (FieldsEqual(field, expected, actual))
                continue;

            mismatches.Add($"{field}: expected={expected ?? "null"}, actual={actual ?? "null"}");
        }

        var passed = mismatches.Count == 0 && compared > 0;
        var detail = passed ? null : string.Join("; ", mismatches);

        return new GoldenEvalOutcome(
            new EvalCaseResult(caseName, passed, detail),
            compared - mismatches.Count,
            compared);
    }

    private static string? GetField(string json, string field)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return field switch
        {
            "counterparty.name" => ReadNestedString(root, "counterparty", "name"),
            "counterparty.inn" => ReadNestedString(root, "counterparty", "inn"),
            "total_amount" => ReadDecimalString(root, "total_amount"),
            _ => ReadScalarString(root, field)
        };
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

    private static bool FieldsEqual(string field, string? expected, string? actual) =>
        field switch
        {
            "counterparty.name" => CounterpartyNamesEqual(expected, actual),
            _ => ValuesEqual(expected, actual)
        };

    /// <summary>The LLM often mixes Latin O and Cyrillic О in «ООО».</summary>
    private static bool CounterpartyNamesEqual(string? expected, string? actual)
    {
        var normExpected = NormalizeCounterpartyName(expected);
        var normActual = NormalizeCounterpartyName(actual);
        return string.Equals(normExpected, normActual, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeCounterpartyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return name
            .Replace('О', 'O')
            .Replace('о', 'o')
            .Replace("«", "", StringComparison.Ordinal)
            .Replace("»", "", StringComparison.Ordinal)
            .Replace("\"", "", StringComparison.Ordinal)
            .Trim();
    }

    private static bool ValuesEqual(string? expected, string? actual)
    {
        if (decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var expDec)
            && decimal.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out var actDec))
        {
            return expDec == actDec;
        }

        return string.Equals(
            Normalize(expected),
            Normalize(actual),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record GoldenEvalOutcome(EvalCaseResult Case, int MatchedFields, int ComparedFields);
