using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class DocumentReconcileServiceTests
{
    private static readonly Guid DocA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DocB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Matching_documents_have_no_discrepancies()
    {
        const string json =
            """
            {
              "doc_type": "счет",
              "number": "2026-041",
              "date": "2026-07-15",
              "counterparty": { "name": "ООО СеверТрейд", "inn": "7701234567" },
              "items": [ { "name": "товар", "quantity": 1, "amount": 1000.0 } ],
              "total_amount": 1000.0,
              "vat_amount": 166.67,
              "currency": "RUB",
              "confidence": 0.9
            }
            """;

        var service = new DocumentReconcileService();
        var outcome = service.Reconcile(
        [
            new DocumentExtractionSnapshot(DocA, "a.pdf", json),
            new DocumentExtractionSnapshot(DocB, "b.pdf", json)
        ]);

        Assert.False(outcome.HasDiscrepancies);
        Assert.Empty(outcome.Discrepancies);
    }

    [Fact]
    public void Different_total_amount_is_reported()
    {
        var service = new DocumentReconcileService();
        var outcome = service.Reconcile(
        [
            new DocumentExtractionSnapshot(DocA, "a.pdf", SampleJson(total: 1000)),
            new DocumentExtractionSnapshot(DocB, "b.pdf", SampleJson(total: 1200))
        ]);

        Assert.True(outcome.HasDiscrepancies);
        Assert.Contains(outcome.Discrepancies, d => d.Field == "total_amount");
    }

    [Fact]
    public void Less_than_two_documents_throws()
    {
        var service = new DocumentReconcileService();
        Assert.Throws<ArgumentException>(() =>
            service.Reconcile([new DocumentExtractionSnapshot(DocA, "a.pdf", SampleJson())]));
    }

    private static string SampleJson(decimal total = 1000) =>
        $$"""
          {
            "doc_type": "счет",
            "number": "1",
            "date": "2026-01-01",
            "counterparty": { "name": "X", "inn": "123" },
            "items": [],
            "total_amount": {{total.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
            "vat_amount": null,
            "currency": "RUB",
            "confidence": 0.9
          }
          """;
}
