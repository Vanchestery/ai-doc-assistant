namespace AiDocAssistant.Core.Abstractions;

/// <summary>
/// Extract text from a document file. Per-type implementations are registered in DI;
/// CompositeDocumentParser routes to the matching one.
/// </summary>
public interface IDocumentParser
{
    bool Supports(string fileName, string contentType);
    Task<ParsedDocument> ExtractTextAsync(string filePath, CancellationToken ct = default);
}

public sealed record ParsedDocument(string Text, bool UsedOcr);
