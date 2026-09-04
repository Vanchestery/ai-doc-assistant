using Microsoft.EntityFrameworkCore;

namespace AiDocAssistant.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

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

        base.OnModelCreating(modelBuilder);
    }
}
