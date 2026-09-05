using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiDocAssistant.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 1: Documents and ExtractionResults tables.
/// The pgvector extension is already enabled by the Initial migration (Phase 0),
/// so this one only adds the tables and relationships.
/// </summary>
public partial class Phase1DocumentTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Documents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Error = table.Column<string>(type: "text", nullable: true),
                ExtractedText = table.Column<string>(type: "text", nullable: true),
                UsedOcr = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Documents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ExtractionResults",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                Json = table.Column<string>(type: "jsonb", nullable: false),
                Confidence = table.Column<double>(type: "double precision", nullable: true),
                Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PromptTokens = table.Column<int>(type: "integer", nullable: false),
                CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExtractionResults", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExtractionResults_Documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "Documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Documents_CreatedAt",
            table: "Documents",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ExtractionResults_DocumentId",
            table: "ExtractionResults",
            column: "DocumentId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ExtractionResults");

        migrationBuilder.DropTable(
            name: "Documents");
    }
}
