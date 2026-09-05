using ai_doc_assistant.Components;
using ai_doc_assistant.Services;
using AiDocAssistant.Core.Abstractions;
using AiDocAssistant.Core.Services;
using AiDocAssistant.Infrastructure.Llm;
using AiDocAssistant.Infrastructure.Parsing;
using AiDocAssistant.Infrastructure.Persistence;
using AiDocAssistant.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true)
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 50 * 1024 * 1024);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<DocumentsApiClient>();
builder.Services.AddScoped<DocumentProcessingService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.UseVector()));

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<DeepSeekOptions>(builder.Configuration.GetSection(DeepSeekOptions.SectionName));

builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<OcrCli>();
builder.Services.AddScoped<IDocumentParser, PdfDocumentParser>();
builder.Services.AddScoped<IDocumentParser, ImageDocumentParser>();
builder.Services.AddScoped<CompositeDocumentParser>();

builder.Services.AddHttpClient<ILlmProvider, DeepSeekLlmProvider>();
builder.Services.AddScoped<DocumentExtractionService>();

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
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
