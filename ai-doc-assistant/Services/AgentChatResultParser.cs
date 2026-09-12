using System.Globalization;
using System.Text.Json;

namespace ai_doc_assistant.Services;

/// <summary>Parse agent tool ResultJson into UI-friendly view models for the chat lane.</summary>
public static class AgentChatResultParser
{
    public static AgentChatResultVm Parse(string tool, string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return AgentChatResultVm.Empty;

        try
        {
            return tool switch
            {
                "reconcile" => ParseReconcile(resultJson),
                "summarize" => ParseSummarize(resultJson),
                "generate_report" => ParseReport(resultJson),
                _ => AgentChatResultVm.Raw(resultJson)
            };
        }
        catch (JsonException)
        {
            return AgentChatResultVm.Raw(resultJson);
        }
    }

    private static AgentChatResultVm ParseReconcile(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : null;
        var hasIssues = root.TryGetProperty("hasDiscrepancies", out var h) && h.GetBoolean();

        var discrepancies = new List<DiscrepancyVm>();
        if (root.TryGetProperty("discrepancies", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var field = item.TryGetProperty("field", out var f) ? f.GetString() ?? "?" : "?";
                var description = item.TryGetProperty("description", out var d) ? d.GetString() : null;
                var values = new List<string>();
                if (item.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in vals.EnumerateArray())
                    {
                        var file = v.TryGetProperty("fileName", out var fn) ? fn.GetString() : "?";
                        var value = v.TryGetProperty("value", out var val) ? val.GetString() ?? "null" : "null";
                        values.Add($"{file}: {value}");
                    }
                }

                discrepancies.Add(new DiscrepancyVm(field, description, values));
            }
        }

        return new AgentChatResultVm(
            Kind: AgentChatResultKind.Reconcile,
            Headline: summary,
            HasIssues: hasIssues,
            Discrepancies: discrepancies,
            Documents: [],
            Report: null,
            RawJson: null);
    }

    private static AgentChatResultVm ParseSummarize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : null;
        var docCount = root.TryGetProperty("documentCount", out var dc) ? dc.GetInt32() : (int?)null;
        var total = root.TryGetProperty("totalAmountSum", out var t) && t.ValueKind == JsonValueKind.Number
            ? t.GetDecimal()
            : (decimal?)null;
        var model = root.TryGetProperty("model", out var m) ? m.GetString() : null;

        var documents = new List<DocBriefVm>();
        if (root.TryGetProperty("documents", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                documents.Add(new DocBriefVm(
                    item.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "?" : "?",
                    item.TryGetProperty("number", out var n) ? n.GetString() : null,
                    item.TryGetProperty("counterparty", out var c) ? c.GetString() : null,
                    item.TryGetProperty("totalAmount", out var ta) && ta.ValueKind == JsonValueKind.Number
                        ? ta.GetDecimal()
                        : null,
                    item.TryGetProperty("currency", out var cur) ? cur.GetString() : null));
            }
        }

        return new AgentChatResultVm(
            Kind: AgentChatResultKind.Summarize,
            Headline: summary,
            HasIssues: false,
            Discrepancies: [],
            Documents: documents,
            Report: new ReportVm(null, docCount, null, total, model, null),
            RawJson: null);
    }

    private static AgentChatResultVm ParseReport(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var fileName = root.TryGetProperty("fileName", out var f) ? f.GetString() : null;
        var docCount = root.TryGetProperty("documentCount", out var dc) ? dc.GetInt32() : (int?)null;
        var itemCount = root.TryGetProperty("itemCount", out var ic) ? ic.GetInt32() : (int?)null;
        var total = root.TryGetProperty("totalAmountSum", out var t) && t.ValueKind == JsonValueKind.Number
            ? t.GetDecimal()
            : (decimal?)null;

        string[]? sheets = null;
        if (root.TryGetProperty("sheets", out var sh) && sh.ValueKind == JsonValueKind.Array)
            sheets = sh.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray();

        var headline = fileName is null
            ? "Excel report ready."
            : $"Excel report ready: {fileName}";

        return new AgentChatResultVm(
            Kind: AgentChatResultKind.Report,
            Headline: headline,
            HasIssues: false,
            Discrepancies: [],
            Documents: [],
            Report: new ReportVm(fileName, docCount, itemCount, total, null, sheets),
            RawJson: null);
    }
}

public enum AgentChatResultKind
{
    Empty,
    Reconcile,
    Summarize,
    Report,
    Raw
}

public sealed record AgentChatResultVm(
    AgentChatResultKind Kind,
    string? Headline,
    bool HasIssues,
    IReadOnlyList<DiscrepancyVm> Discrepancies,
    IReadOnlyList<DocBriefVm> Documents,
    ReportVm? Report,
    string? RawJson)
{
    public static AgentChatResultVm Empty { get; } = new(
        AgentChatResultKind.Empty, null, false, [], [], null, null);

    public static AgentChatResultVm Raw(string json) => new(
        AgentChatResultKind.Raw, null, false, [], [], null, json);
}

public sealed record DiscrepancyVm(string Field, string? Description, IReadOnlyList<string> Values);

public sealed record DocBriefVm(
    string FileName,
    string? Number,
    string? Counterparty,
    decimal? TotalAmount,
    string? Currency);

public sealed record ReportVm(
    string? FileName,
    int? DocumentCount,
    int? ItemCount,
    decimal? TotalAmountSum,
    string? Model,
    IReadOnlyList<string>? Sheets);

public static class AgentChatMoney
{
    public static string Format(decimal? value, string? currency = null)
    {
        if (value is null)
            return "—";

        var number = value.Value.ToString("N2", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(currency) ? number : $"{number} {currency}";
    }
}
