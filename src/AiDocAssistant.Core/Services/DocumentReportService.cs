using System.Globalization;
using System.Text.Json;

namespace AiDocAssistant.Core.Services;

/// <summary>
/// Dataset for an xlsx report from ExtractionResult.Json — no LLM and no database.
/// </summary>
public sealed class DocumentReportService
{
    public ReportDataset Build(IReadOnlyList<DocumentReportInput> documents)
    {
        if (documents.Count == 0)
            throw new ArgumentException("At least one document is required.", nameof(documents));

        var docRows = new List<ReportDocumentRow>();
        var itemRows = new List<ReportItemRow>();

        foreach (var doc in documents)
        {
            using var json = JsonDocument.Parse(doc.ExtractionJson);
            var root = json.RootElement;

            docRows.Add(ExtractDocumentRow(doc, root));
            itemRows.AddRange(ExtractItemRows(doc.FileName, root));
        }

        return new ReportDataset(docRows, itemRows);
    }

    private static ReportDocumentRow ExtractDocumentRow(DocumentReportInput doc, JsonElement root)
    {
        var itemsCount = root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.GetArrayLength()
            : 0;

        return new ReportDocumentRow(
            doc.DocumentId,
            doc.FileName,
            ReadString(root, "doc_type"),
            ReadString(root, "number"),
            ReadString(root, "date"),
            ReadNestedString(root, "counterparty", "name"),
            ReadNestedString(root, "counterparty", "inn"),
            ReadDecimal(root, "total_amount"),
            ReadDecimal(root, "vat_amount"),
            ReadString(root, "currency"),
            itemsCount);
    }

    private static IEnumerable<ReportItemRow> ExtractItemRows(string fileName, JsonElement root)
    {
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            yield break;

        var line = 1;
        foreach (var item in items.EnumerateArray())
        {
            yield return new ReportItemRow(
                fileName,
                line++,
                ReadString(item, "name"),
                ReadDecimal(item, "quantity"),
                ReadString(item, "unit"),
                ReadDecimal(item, "unit_price"),
                ReadDecimal(item, "amount"));
        }
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

        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        };
    }
}

public sealed record DocumentReportInput(Guid DocumentId, string FileName, string ExtractionJson);

public sealed record ReportDocumentRow(
    Guid DocumentId,
    string FileName,
    string? DocType,
    string? Number,
    string? Date,
    string? CounterpartyName,
    string? CounterpartyInn,
    decimal? TotalAmount,
    decimal? VatAmount,
    string? Currency,
    int ItemsCount);

public sealed record ReportItemRow(
    string FileName,
    int LineNumber,
    string? Name,
    decimal? Quantity,
    string? Unit,
    decimal? UnitPrice,
    decimal? Amount);

public sealed record ReportDataset(
    IReadOnlyList<ReportDocumentRow> Documents,
    IReadOnlyList<ReportItemRow> Items);
