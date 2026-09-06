using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AiDocAssistant.Infrastructure.Persistence;

/// <summary>
/// Chunk store on pgvector on top of EF Core.
/// Search uses the cosine distance operator (&lt;=&gt;) via CosineDistance —
/// EF translates it to SQL and the query hits the HNSW index. See DECISIONS.md #14.
/// </summary>
public sealed class PgVectorChunkStore : IChunkStore
{
    private readonly AppDbContext _db;

    public PgVectorChunkStore(AppDbContext db) => _db = db;

    public async Task ReplaceForDocumentAsync(Guid documentId, IReadOnlyList<ChunkRecord> chunks, CancellationToken ct = default)
    {
        var existing = _db.Chunks.Where(c => c.DocumentId == documentId);
        _db.Chunks.RemoveRange(existing);

        foreach (var r in chunks)
        {
            _db.Chunks.Add(new Chunk
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Ordinal = r.Ordinal,
                Text = r.Text,
                Embedding = new Vector(r.Embedding),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ChunkHit>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        Guid? documentId = null,
        CancellationToken ct = default)
    {
        var query = new Vector(queryEmbedding);

        var q = _db.Chunks.AsQueryable();
        if (documentId is not null)
            q = q.Where(c => c.DocumentId == documentId);

        return await q
            .OrderBy(c => c.Embedding.CosineDistance(query))
            .Take(topK)
            .Join(
                _db.Documents,
                c => c.DocumentId,
                d => d.Id,
                (c, d) => new ChunkHit(
                    c.DocumentId,
                    d.FileName,
                    c.Ordinal,
                    c.Text,
                    c.Embedding.CosineDistance(query)))
            .ToListAsync(ct);
    }
}
