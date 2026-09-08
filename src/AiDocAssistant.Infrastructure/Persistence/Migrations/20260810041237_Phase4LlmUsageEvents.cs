using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiDocAssistant.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 4: LlmUsageEvents table for per-call token, latency, and cost telemetry.
/// </summary>
public partial class Phase4LlmUsageEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LlmUsageEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PromptTokens = table.Column<int>(type: "integer", nullable: false),
                CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                EstimatedCostUsd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LlmUsageEvents", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LlmUsageEvents_CreatedAt",
            table: "LlmUsageEvents",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_LlmUsageEvents_Operation",
            table: "LlmUsageEvents",
            column: "Operation");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LlmUsageEvents");
    }
}
