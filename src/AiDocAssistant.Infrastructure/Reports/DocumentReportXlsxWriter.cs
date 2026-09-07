using AiDocAssistant.Core.Services;
using ClosedXML.Excel;

namespace AiDocAssistant.Infrastructure.Reports;

/// <summary>Write a ReportDataset to xlsx (ClosedXML).</summary>
public sealed class DocumentReportXlsxWriter
{
    public byte[] Write(ReportDataset data)
    {
        using var workbook = new XLWorkbook();
        WriteDocumentsSheet(workbook, data.Documents);
        WriteItemsSheet(workbook, data.Items);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteDocumentsSheet(XLWorkbook workbook, IReadOnlyList<ReportDocumentRow> documents)
    {
        var sheet = workbook.Worksheets.Add("Документы");

        var headers = new[]
        {
            "Файл", "Тип", "Номер", "Дата", "Контрагент", "ИНН",
            "Сумма", "НДС", "Валюта", "Позиций"
        };

        for (var col = 0; col < headers.Length; col++)
            sheet.Cell(1, col + 1).Value = headers[col];

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        var row = 2;
        foreach (var doc in documents)
        {
            sheet.Cell(row, 1).Value = doc.FileName;
            sheet.Cell(row, 2).Value = doc.DocType ?? string.Empty;
            sheet.Cell(row, 3).Value = doc.Number ?? string.Empty;
            sheet.Cell(row, 4).Value = doc.Date ?? string.Empty;
            sheet.Cell(row, 5).Value = doc.CounterpartyName ?? string.Empty;
            sheet.Cell(row, 6).Value = doc.CounterpartyInn ?? string.Empty;
            WriteDecimalCell(sheet.Cell(row, 7), doc.TotalAmount);
            WriteDecimalCell(sheet.Cell(row, 8), doc.VatAmount);
            sheet.Cell(row, 9).Value = doc.Currency ?? string.Empty;
            sheet.Cell(row, 10).Value = doc.ItemsCount;
            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void WriteItemsSheet(XLWorkbook workbook, IReadOnlyList<ReportItemRow> items)
    {
        var sheet = workbook.Worksheets.Add("Позиции");

        var headers = new[] { "Файл", "№", "Наименование", "Кол-во", "Ед.", "Цена", "Сумма" };

        for (var col = 0; col < headers.Length; col++)
            sheet.Cell(1, col + 1).Value = headers[col];

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.FileName;
            sheet.Cell(row, 2).Value = item.LineNumber;
            sheet.Cell(row, 3).Value = item.Name ?? string.Empty;
            WriteDecimalCell(sheet.Cell(row, 4), item.Quantity);
            sheet.Cell(row, 5).Value = item.Unit ?? string.Empty;
            WriteDecimalCell(sheet.Cell(row, 6), item.UnitPrice);
            WriteDecimalCell(sheet.Cell(row, 7), item.Amount);
            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void WriteDecimalCell(IXLCell cell, decimal? value)
    {
        if (value is null)
            return;

        cell.Value = value.Value;
        cell.Style.NumberFormat.Format = "#,##0.00";
    }
}
