using AiDocAssistant.Core.Abstractions;

namespace AiDocAssistant.Core.Services;

/// <summary>
/// Offline RAG retrieval eval: is the top-1 hit relevant to the question (recorded hits).
/// </summary>
public static class RagRetrievalEval
{
    public static EvalCaseResult EvaluateTopHit(
        string caseName,
        IReadOnlyList<ChunkHit> hits,
        string expectedTextFragment,
        string? expectedFileNameFragment = null)
    {
        if (hits.Count == 0)
            return new EvalCaseResult(caseName, false, "no hits");

        var top = hits.OrderBy(h => h.Distance).First();

        var textOk = top.Text.Contains(expectedTextFragment, StringComparison.OrdinalIgnoreCase);
        var fileOk = expectedFileNameFragment is null
            || top.DocumentFileName.Contains(expectedFileNameFragment, StringComparison.OrdinalIgnoreCase);

        var passed = textOk && fileOk;
        var detail = passed
            ? null
            : $"top={top.DocumentFileName}, distance={top.Distance:F3}, excerpt={Truncate(top.Text)}";

        return new EvalCaseResult(caseName, passed, detail);
    }

    /// <summary>Recorded scenarios from the demo PDFs (sample invoice A/B).</summary>
    public static IEnumerable<EvalCaseResult> RunRecordedScenarios()
    {
        yield return EvaluateTopHit(
            "rag.retrieval.invoice_a_total",
            InvoiceAHits(),
            expectedTextFragment: "112700",
            expectedFileNameFragment: "112700");

        yield return EvaluateTopHit(
            "rag.retrieval.invoice_b_total",
            InvoiceBHits(),
            expectedTextFragment: "115000",
            expectedFileNameFragment: "115000");
    }

    private static IReadOnlyList<ChunkHit> InvoiceAHits() =>
    [
        new(
            Guid.Parse("aaaaaaaa-1111-2222-3333-444444444441"),
            "sample-invoice-a-112700.pdf",
            0,
            "SCHET No 2026-041 Itogo: 112700.00 RUB NDS: 18783.33",
            0.08),
        new(
            Guid.Parse("bbbbbbbb-1111-2222-3333-444444444442"),
            "sample-invoice-b-115000.pdf",
            0,
            "Other invoice total 115000",
            0.91)
    ];

    private static IReadOnlyList<ChunkHit> InvoiceBHits() =>
    [
        new(
            Guid.Parse("bbbbbbbb-1111-2222-3333-444444444442"),
            "sample-invoice-b-115000.pdf",
            0,
            "SCHET No 2026-041 Itogo: 115000.00 RUB NDS: 19166.67",
            0.07),
        new(
            Guid.Parse("aaaaaaaa-1111-2222-3333-444444444441"),
            "sample-invoice-a-112700.pdf",
            0,
            "Other invoice total 112700",
            0.88)
    ];

    private static string Truncate(string text) =>
        text.Length <= 80 ? text : text[..80] + "…";
}
