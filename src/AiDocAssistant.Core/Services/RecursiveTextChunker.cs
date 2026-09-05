using AiDocAssistant.Core.Abstractions;

namespace AiDocAssistant.Core.Services;

/// <summary>
/// Recursive chunker over a separator hierarchy (paragraph → line → sentence → word → character).
/// Splits on the largest natural boundary that still fits in MaxChars,
/// and merges small pieces into sized chunks with overlap.
/// Same idea as LangChain RecursiveCharacterTextSplitter — the usual RAG default.
/// See DECISIONS.md #12.
/// </summary>
public sealed class RecursiveTextChunker : ITextChunker
{
    // Coarse to fine. Empty string at the end = split by character (last resort).
    private static readonly string[] Separators = ["\n\n", "\n", ". ", " ", ""];

    public IReadOnlyList<TextChunk> Chunk(string text, ChunkingOptions? options = null)
    {
        options ??= ChunkingOptions.Default;
        if (options.Overlap >= options.MaxChars)
            throw new ArgumentException("Overlap must be less than MaxChars.", nameof(options));

        if (string.IsNullOrWhiteSpace(text))
            return [];

        var pieces = SplitRecursive(text.Trim(), Separators, options);

        var chunks = new List<TextChunk>(pieces.Count);
        var index = 0;
        foreach (var piece in pieces)
        {
            var trimmed = piece.Trim();
            if (trimmed.Length > 0)
                chunks.Add(new TextChunk(index++, trimmed));
        }
        return chunks;
    }

    /// <summary>
    /// Pick the coarsest separator present in the text and split on it.
    /// Pieces under MaxChars are collected and merged; larger pieces are split
    /// recursively with the next (finer) separator.
    /// </summary>
    private static List<string> SplitRecursive(string text, string[] separators, ChunkingOptions options)
    {
        var result = new List<string>();

        var separator = separators[^1];
        var remaining = Array.Empty<string>();
        for (var i = 0; i < separators.Length; i++)
        {
            var s = separators[i];
            if (s.Length == 0) { separator = s; break; }
            if (text.Contains(s, StringComparison.Ordinal))
            {
                separator = s;
                remaining = separators[(i + 1)..];
                break;
            }
        }

        var splits = separator.Length == 0
            ? text.Select(c => c.ToString()).ToArray()
            : text.Split(separator).Where(s => s.Length > 0).ToArray();

        var good = new List<string>();
        foreach (var part in splits)
        {
            if (part.Length < options.MaxChars)
            {
                good.Add(part);
                continue;
            }

            // Merge accumulated small pieces, then split the large piece deeper.
            if (good.Count > 0)
            {
                result.AddRange(MergeWithOverlap(good, separator, options));
                good.Clear();
            }

            if (remaining.Length == 0)
                result.Add(part);
            else
                result.AddRange(SplitRecursive(part, remaining, options));
        }

        if (good.Count > 0)
            result.AddRange(MergeWithOverlap(good, separator, options));

        return result;
    }

    /// <summary>
    /// Greedily pack pieces (joined with their separator) into chunks up to MaxChars.
    /// On overflow, slide the window and keep a tail of about Overlap
    /// so neighboring chunks overlap and context does not break at the boundary.
    /// </summary>
    private static IEnumerable<string> MergeWithOverlap(IReadOnlyList<string> splits, string separator, ChunkingOptions options)
    {
        var sepLen = separator.Length;
        var docs = new List<string>();
        var window = new List<string>();
        var total = 0;

        foreach (var part in splits)
        {
            var addSep = window.Count > 0 ? sepLen : 0;

            if (total + part.Length + addSep > options.MaxChars && window.Count > 0)
            {
                var doc = string.Join(separator, window).Trim();
                if (doc.Length > 0)
                    docs.Add(doc);

                // Slide the left edge until we fit the limit and have cut the overlap.
                while (total > options.Overlap ||
                       (total + part.Length + (window.Count > 0 ? sepLen : 0) > options.MaxChars && total > 0))
                {
                    total -= window[0].Length + (window.Count > 1 ? sepLen : 0);
                    window.RemoveAt(0);
                    if (window.Count == 0) break;
                }
            }

            window.Add(part);
            total += part.Length + (window.Count > 1 ? sepLen : 0);
        }

        var last = string.Join(separator, window).Trim();
        if (last.Length > 0)
            docs.Add(last);

        return docs;
    }
}
