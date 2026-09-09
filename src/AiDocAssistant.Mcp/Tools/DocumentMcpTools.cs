using System.ComponentModel;
using System.Text.Json;
using AiDocAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace AiDocAssistant.Mcp.Tools;

/// <summary>MCP tools over uploaded documents and their extraction JSON.</summary>
[McpServerToolType]
public sealed class DocumentMcpTools
{
    [McpServerTool, Description("List uploaded documents (id, file name, status, size, createdAt).")]
    public static async Task<string> ListDocuments(AppDbContext db, CancellationToken cancellationToken)
    {
        var items = await db.Documents
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.FileName,
                Status = d.Status.ToString(),
                d.UsedOcr,
                d.SizeBytes,
                d.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(items, McpJson.Options);
    }

    [McpServerTool, Description("Get document details including extraction JSON by document id.")]
    public static async Task<string> GetDocument(
        AppDbContext db,
        [Description("Document GUID from list_documents.")] Guid documentId,
        CancellationToken cancellationToken)
    {
        var doc = await db.Documents
            .Include(d => d.Extraction)
            .Where(d => d.Id == documentId)
            .Select(d => new
            {
                d.Id,
                d.FileName,
                Status = d.Status.ToString(),
                d.UsedOcr,
                d.SizeBytes,
                d.CreatedAt,
                d.Error,
                Extraction = d.Extraction == null
                    ? null
                    : new
                    {
                        d.Extraction.Json,
                        d.Extraction.Confidence,
                        d.Extraction.Model,
                        d.Extraction.PromptTokens,
                        d.Extraction.CompletionTokens
                    }
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (doc is null)
            return JsonSerializer.Serialize(new { error = $"Document {documentId} not found." }, McpJson.Options);

        return JsonSerializer.Serialize(doc, McpJson.Options);
    }
}
