using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiDocAssistant.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 2: ChatSessions and ChatMessages for RAG conversations.
/// Assistant citations are stored as jsonb on ChatMessages.
/// </summary>
public partial class Phase2ChatSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChatSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatSessions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ChatMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<int>(type: "integer", nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                CitationsJson = table.Column<string>(type: "jsonb", nullable: true),
                Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                PromptTokens = table.Column<int>(type: "integer", nullable: true),
                CompletionTokens = table.Column<int>(type: "integer", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChatMessages_ChatSessions_SessionId",
                    column: x => x.SessionId,
                    principalTable: "ChatSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_CreatedAt",
            table: "ChatMessages",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_SessionId",
            table: "ChatMessages",
            column: "SessionId");

        migrationBuilder.CreateIndex(
            name: "IX_ChatSessions_CreatedAt",
            table: "ChatSessions",
            column: "CreatedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ChatMessages");

        migrationBuilder.DropTable(
            name: "ChatSessions");
    }
}
