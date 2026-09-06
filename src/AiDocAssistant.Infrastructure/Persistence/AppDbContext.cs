using AiDocAssistant.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiDocAssistant.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ExtractionResult> ExtractionResults => Set<ExtractionResult>();
    public DbSet<Chunk> Chunks => Set<Chunk>();

    /// <summary>
    /// Embedding dimension of the model (bge-m3 = 1024). Changing to a model
    /// with a different size requires a new migration for vector(N). See DECISIONS.md #14.
    /// </summary>
    public const int EmbeddingDimensions = 1024;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Enable the pgvector extension on the model:
        // EF emits CREATE EXTENSION in the migration so the schema
        // can be reproduced with one command on any machine.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Document>(b =>
        {
            b.Property(x => x.FileName).HasMaxLength(500);
            b.Property(x => x.ContentType).HasMaxLength(200);
            b.Property(x => x.StoragePath).HasMaxLength(1000);
            b.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<ExtractionResult>(b =>
        {
            b.Property(x => x.Json).HasColumnType("jsonb");
            b.Property(x => x.Model).HasMaxLength(200);
            b.HasOne(x => x.Document)
                .WithOne(x => x.Extraction)
                .HasForeignKey<ExtractionResult>(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Chunk>(b =>
        {
            b.Property(x => x.Text).HasColumnType("text");
            b.Property(x => x.Embedding).HasColumnType($"vector({EmbeddingDimensions})");

            // HNSW index with the cosine operator — fast ANN nearest-vector search.
            b.HasIndex(x => x.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            b.HasIndex(x => x.DocumentId);

            b.HasOne<Document>()
                .WithMany()
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
