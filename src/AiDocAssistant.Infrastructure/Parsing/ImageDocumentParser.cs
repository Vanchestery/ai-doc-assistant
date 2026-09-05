using AiDocAssistant.Core.Abstractions;

namespace AiDocAssistant.Infrastructure.Parsing;

/// <summary>Images (photos/scans): OCR immediately.</summary>
public class ImageDocumentParser : IDocumentParser
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"];

    private readonly OcrCli _ocr;

    public ImageDocumentParser(OcrCli ocr) => _ocr = ocr;

    public bool Supports(string fileName, string contentType) =>
        Extensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    public async Task<ParsedDocument> ExtractTextAsync(string filePath, CancellationToken ct = default)
    {
        var text = await _ocr.RecognizeImageAsync(filePath, ct);
        return new ParsedDocument(text, UsedOcr: true);
    }
}
