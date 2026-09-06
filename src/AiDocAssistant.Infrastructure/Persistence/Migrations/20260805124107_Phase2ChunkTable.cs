using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace AiDocAssistant.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 2: Chunks table for RAG — text plus a vector(1024) embedding
/// and an HNSW cosine index. The pgvector extension is already enabled
/// by the Initial migration.
/// </summary>
public partial class Phase2ChunkTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Chunks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                Ordinal = table.Column<int>(type: "integer", nullable: false),
                Text = table.Column<string>(type: "text", nullable: false),
                Embedding = table.Column<Vector>(type: "vector(1024)", nullable: false),
                PageNumber = table.Column<int>(type: "integer", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Chunks", x => x.Id);
                table.ForeignKey(
                    name: "FK_Chunks_Documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "Documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Chunks_DocumentId",
            table: "Chunks",
            column: "DocumentId");

        migrationBuilder.CreateIndex(
            name: "IX_Chunks_Embedding",
            table: "Chunks",
            column: "Embedding")
            .Annotation("Npgsql:IndexMethod", "hnsw")
            .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Chunks");
    }
}
