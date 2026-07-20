using System.Text.Json;
using GraphRag;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyGraphRagV5.Ai;
using MyGraphRagV5.Data;
using MyGraphRagV5.Health;
using MyGraphRagV5.Indexing;
using MyGraphRagV5.Query;
using MyGraphRagV5.Vectors;

var builder = WebApplication.CreateBuilder(args);

// The default environment-variable configuration provider only maps OLLAMA_API_KEY to
// "Ollama:ApiKey" via the double-underscore convention (Ollama__ApiKey). Bridge the literal
// env var name required by the Global Constraints without ever hardcoding the key itself.
var ollamaApiKeyEnv = Environment.GetEnvironmentVariable("OLLAMA_API_KEY");
if (!string.IsNullOrEmpty(ollamaApiKeyEnv) && string.IsNullOrEmpty(builder.Configuration["Ollama:ApiKey"]))
{
    builder.Configuration["Ollama:ApiKey"] = ollamaApiKeyEnv;
}

var connectionString = builder.Configuration.GetConnectionString("GraphDb")
    ?? throw new InvalidOperationException("Connection string 'GraphDb' is not configured.");

builder.Services.AddAppDatabase(connectionString);
builder.Services.AddPgVectorStore(connectionString);
builder.Services.AddAiClients(builder.Configuration);
builder.Services.AddGraphRag();
builder.Services.AddIndexingServices();
builder.Services.AddRagQuery();
builder.Services.AddRazorPages();

// Used by the Ollama/TEI health checks (IHttpClientFactory.CreateClient()).
builder.Services.AddHttpClient();

builder.Services.AddHealthChecks()
    .AddCheck("postgres", new PostgresHealthCheck(connectionString))
    .AddCheck<OllamaHealthCheck>("ollama")
    .AddTypeActivatedCheck<TeiHealthCheck>("tei-embed", args: [TeiEndpointKind.Embed])
    .AddTypeActivatedCheck<TeiHealthCheck>("tei-rerank", args: [TeiEndpointKind.Rerank]);

var app = builder.Build();

// Fail fast: an app that cannot migrate its own schema is not usable, so log clearly and rethrow
// rather than starting in a half-working state.
using (var startupScope = app.Services.CreateScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = startupScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Database migration failed for connection string 'GraphDb'. Verify the database at {ConnectionHost} is reachable.", connectionString);
        throw;
    }
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = WriteHealthReportAsync,
});

app.Run();

static async Task WriteHealthReportAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        results = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
        }),
    };
    await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}
