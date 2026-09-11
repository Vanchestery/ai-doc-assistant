using AiDocAssistant.Core.Services;
using AiDocAssistant.Infrastructure.Reports;
using ClosedXML.Excel;
using Xunit;

namespace AiDocAssistant.Tests;

public class DocumentReportServiceTests
{
    private static readonly Guid DocId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private const string SampleJson =
        """
        {
          "doc_type": "счет",
          "number": "2026-041",
          "date": "2026-07-15",
          "counterparty": { "name": "ООО КофеPoint", "inn": "7701234567" },
          "items": [
            { "name": "Кофе", "quantity": 40, "unit": "кг", "unit_price": 1250.0, "amount": 50000.0 },
            { "name": "Эспрессо", "quantity": 120, "unit": "шт", "unit_price": 320.0, "amount": 38400.0 }
          ],
          "total_amount": 112700.0,
          "vat_amount": 18783.33,
          "currency": "RUB",
          "confidence": 0.9
        }
        """;

    [Fact]
    public void Build_extracts_document_and_item_rows()
    {
        var service = new DocumentReportService();
        var dataset = service.Build(
        [
            new DocumentReportInput(DocId, "schet.pdf", SampleJson)
        ]);

        Assert.Single(dataset.Documents);
        Assert.Equal(2, dataset.Items.Count);
        Assert.Equal("2026-041", dataset.Documents[0].Number);
        Assert.Equal(112700m, dataset.Documents[0].TotalAmount);
        Assert.Equal("Кофе", dataset.Items[0].Name);
    }

    [Fact]
    public void Empty_documents_throw()
    {
        var service = new DocumentReportService();
        Assert.Throws<ArgumentException>(() => service.Build([]));
    }

    [Fact]
    public void Writer_produces_xlsx_with_two_sheets()
    {
        var service = new DocumentReportService();
        var dataset = service.Build(
        [
            new DocumentReportInput(DocId, "schet.pdf", SampleJson)
        ]);

        var bytes = new DocumentReportXlsxWriter().Write(dataset);

        Assert.True(bytes.Length > 100);

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);

        Assert.Equal(2, workbook.Worksheets.Count);
        Assert.Equal("Документы", workbook.Worksheet(1).Name);
        Assert.Equal("Позиции", workbook.Worksheet(2).Name);
        Assert.Equal("schet.pdf", workbook.Worksheet(1).Cell(2, 1).GetString());
    }
}
