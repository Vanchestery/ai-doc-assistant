using ai_doc_assistant.Components;
using ai_doc_assistant.Services;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Services;
using AiDocAssistant.Infrastructure.Agent;
using AiDocAssistant.Infrastructure.Llm;
using AiDocAssistant.Infrastructure.Parsing;
using AiDocAssistant.Infrastructure.Persistence;
using AiDocAssistant.Infrastructure.Reports;
using AiDocAssistant.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true)
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 50 * 1024 * 1024);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<DocumentsApiClient>();
builder.Services.AddScoped<ChatApiClient>();
builder.Services.AddScoped<AgentApiClient>();
builder.Services.AddScoped<MetricsApiClient>();
builder.Services.AddScoped<DocumentProcessingService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.UseVector()));

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<DeepSeekOptions>(builder.Configuration.GetSection(DeepSeekOptions.SectionName));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.SectionName));
builder.Services.Configure<RagOptions>(builder.Configuration.GetSection(RagOptions.SectionName));
builder.Services.AddSingleton(_ =>
{
    var options = new RagOptions();
    builder.Configuration.GetSection(RagOptions.SectionName).Bind(options);
    return options;
});

builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<OcrCli>();
builder.Services.AddScoped<IDocumentParser, PdfDocumentParser>();
builder.Services.AddScoped<IDocumentParser, ImageDocumentParser>();
builder.Services.AddScoped<CompositeDocumentParser>();

builder.Services.Configure<LlmPricingOptions>(builder.Configuration.GetSection(LlmPricingOptions.SectionName));
builder.Services.AddSingleton<LlmCostEstimator>(sp =>
    new LlmCostEstimator(sp.GetRequiredService<IOptions<LlmPricingOptions>>().Value));
builder.Services.AddHttpClient<DeepSeekLlmProvider>();
builder.Services.AddScoped<ILlmUsageStore, EfLlmUsageStore>();
builder.Services.AddScoped<ILlmProvider>(sp => new MeteringLlmProvider(
    sp.GetRequiredService<DeepSeekLlmProvider>(),
    sp.GetRequiredService<ILlmUsageStore>(),
    sp.GetRequiredService<LlmCostEstimator>()));
builder.Services.AddScoped<DocumentExtractionService>();

builder.Services.AddSingleton<ITextChunker, RecursiveTextChunker>();
builder.Services.AddHttpClient<IEmbeddingProvider, OpenAiCompatibleEmbeddingProvider>();
builder.Services.AddScoped<IChunkStore, PgVectorChunkStore>();
builder.Services.AddScoped<DocumentIndexingService>();

builder.Services.AddScoped<IChatSessionStore, EfChatSessionStore>();
builder.Services.AddScoped<RagChatService>();

builder.Services.AddSingleton<DocumentReconcileService>();
builder.Services.AddSingleton<DocumentReportService>();
builder.Services.AddSingleton<DocumentReportXlsxWriter>();
builder.Services.AddScoped<DocumentSummarizeService>();
builder.Services.AddScoped<IAgentTaskStore, EfAgentTaskStore>();
builder.Services.AddScoped<IAgentTool, ReconcileAgentTool>();
builder.Services.AddScoped<IAgentTool, SummarizeAgentTool>();
builder.Services.AddScoped<IAgentTool, GenerateReportAgentTool>();
builder.Services.AddScoped<AgentToolRegistry>();
builder.Services.AddScoped<AgentGoalRouterService>();
builder.Services.AddScoped<AgentGoalService>();
builder.Services.AddScoped<AgentTaskService>();

builder.Services.AddScoped<IDataCountsProvider, EfDataCountsProvider>();
builder.Services.AddSingleton<EvalSuiteService>();
builder.Services.AddScoped<MetricsService>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Demo trade-off (see DECISIONS.md): auto-migrate on startup for a single replica.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
