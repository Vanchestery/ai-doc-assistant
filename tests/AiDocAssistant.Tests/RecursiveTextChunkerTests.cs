using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Services;
using Xunit;

namespace AiDocAssistant.Tests;

public class RecursiveTextChunkerTests
{
    private readonly RecursiveTextChunker _chunker = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n  \n")]
    public void Empty_or_whitespace_yields_no_chunks(string text)
    {
        var chunks = _chunker.Chunk(text);
        Assert.Empty(chunks);
    }

    [Fact]
    public void Short_text_becomes_single_chunk()
    {
        const string text = "Счёт № 2026-0417 от 15 июля 2026 г.";

        var chunks = _chunker.Chunk(text, new ChunkingOptions(MaxChars: 1000, Overlap: 100));

        var chunk = Assert.Single(chunks);
        Assert.Equal(0, chunk.Index);
        Assert.Equal(text, chunk.Text);
    }

    [Fact]
    public void Long_text_is_split_into_multiple_chunks()
    {
        var text = string.Join(". ", Enumerable.Range(1, 60).Select(i => $"Позиция номер {i} на сумму {i * 100} рублей"));

        var chunks = _chunker.Chunk(text, new ChunkingOptions(MaxChars: 200, Overlap: 40));

        Assert.True(chunks.Count > 1, "длинный текст должен разбиться на несколько чанков");
    }

    [Fact]
    public void Chunks_respect_max_size_when_separators_allow()
    {
        // Текст с частыми пробелами — чанкер всегда может уложиться в лимит.
        var text = string.Join(" ", Enumerable.Range(1, 500).Select(i => $"слово{i}"));
        var options = new ChunkingOptions(MaxChars: 150, Overlap: 30);

        var chunks = _chunker.Chunk(text, options);

        Assert.All(chunks, c => Assert.True(
            c.CharCount <= options.MaxChars,
            $"чанк {c.Index} длиной {c.CharCount} превысил MaxChars={options.MaxChars}"));
    }

    [Fact]
    public void Consecutive_chunks_overlap()
    {
        var text = string.Join(" ", Enumerable.Range(1, 300).Select(i => $"w{i}"));
        var options = new ChunkingOptions(MaxChars: 100, Overlap: 30);

        var chunks = _chunker.Chunk(text, options);

        Assert.True(chunks.Count >= 2);
        // Хвост предыдущего чанка должен встречаться в начале следующего.
        var prevTailWord = chunks[0].Text.Split(' ')[^1];
        Assert.Contains(prevTailWord, chunks[1].Text);
    }

    [Fact]
    public void Indexes_are_sequential_from_zero()
    {
        var text = string.Join(" ", Enumerable.Range(1, 400).Select(i => $"токен{i}"));

        var chunks = _chunker.Chunk(text, new ChunkingOptions(MaxChars: 120, Overlap: 20));

        for (var i = 0; i < chunks.Count; i++)
            Assert.Equal(i, chunks[i].Index);
    }

    [Fact]
    public void Overlap_greater_or_equal_to_max_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _chunker.Chunk("любой текст", new ChunkingOptions(MaxChars: 100, Overlap: 100)));
    }
}
