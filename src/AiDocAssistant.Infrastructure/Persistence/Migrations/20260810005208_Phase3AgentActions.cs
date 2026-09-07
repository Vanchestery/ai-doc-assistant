using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiDocAssistant.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 3: AgentActions table for reconcile / summarize / generate_report tasks.
/// Status and timestamps live on the row; input and result are jsonb.
/// </summary>
public partial class Phase3AgentActions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AgentActions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Tool = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                InputJson = table.Column<string>(type: "jsonb", nullable: false),
                ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                Error = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgentActions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AgentActions_CreatedAt",
            table: "AgentActions",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_AgentActions_Status",
            table: "AgentActions",
            column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AgentActions");
    }
}
