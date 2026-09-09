using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Services;
using AiDocAssistant.Infrastructure.Agent;
using AiDocAssistant.Infrastructure.Llm;
using AiDocAssistant.Infrastructure.Parsing;
using AiDocAssistant.Infrastructure.Persistence;
using AiDocAssistant.Infrastructure.Reports;
using AiDocAssistant.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace AiDocAssistant.Mcp;

/// <summary>DI for the MCP host: the same Core/Infrastructure services as the web app, without Blazor.</summary>
internal static class ServiceRegistration
{
    public static IServiceCollection AddAiDocAssistantServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.UseVector()));

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<DeepSeekOptions>(configuration.GetSection(DeepSeekOptions.SectionName));
        services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.SectionName));
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));
        services.AddSingleton(_ =>
        {
            var options = new RagOptions();
            configuration.GetSection(RagOptions.SectionName).Bind(options);
            return options;
        });

        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<OcrCli>();
        services.AddScoped<IDocumentParser, PdfDocumentParser>();
        services.AddScoped<IDocumentParser, ImageDocumentParser>();
        services.AddScoped<CompositeDocumentParser>();

        services.Configure<LlmPricingOptions>(configuration.GetSection(LlmPricingOptions.SectionName));
        services.AddSingleton<LlmCostEstimator>(sp =>
            new LlmCostEstimator(sp.GetRequiredService<IOptions<LlmPricingOptions>>().Value));
        services.AddHttpClient<DeepSeekLlmProvider>();
        services.AddScoped<ILlmUsageStore, EfLlmUsageStore>();
        services.AddScoped<ILlmProvider>(sp => new MeteringLlmProvider(
            sp.GetRequiredService<DeepSeekLlmProvider>(),
            sp.GetRequiredService<ILlmUsageStore>(),
            sp.GetRequiredService<LlmCostEstimator>()));
        services.AddScoped<DocumentExtractionService>();

        services.AddSingleton<ITextChunker, RecursiveTextChunker>();
        services.AddHttpClient<IEmbeddingProvider, OpenAiCompatibleEmbeddingProvider>();
        services.AddScoped<IChunkStore, PgVectorChunkStore>();
        services.AddScoped<DocumentIndexingService>();

        services.AddScoped<IChatSessionStore, EfChatSessionStore>();
        services.AddScoped<RagChatService>();

        services.AddSingleton<DocumentReconcileService>();
        services.AddSingleton<DocumentReportService>();
        services.AddSingleton<DocumentReportXlsxWriter>();
        services.AddScoped<DocumentSummarizeService>();
        services.AddScoped<IAgentTaskStore, EfAgentTaskStore>();
        services.AddScoped<IAgentTool, ReconcileAgentTool>();
        services.AddScoped<IAgentTool, SummarizeAgentTool>();
        services.AddScoped<IAgentTool, GenerateReportAgentTool>();
        services.AddScoped<AgentToolRegistry>();
        services.AddScoped<AgentGoalRouterService>();
        services.AddScoped<AgentGoalService>();
        services.AddScoped<AgentTaskService>();

        services.AddScoped<IDataCountsProvider, EfDataCountsProvider>();
        services.AddSingleton<EvalSuiteService>();
        services.AddScoped<MetricsService>();

        return services;
    }
}
