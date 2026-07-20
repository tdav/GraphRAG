using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MyGraphRagV5.Pages.Health;

/// <summary>
/// Server-rendered health panel, polled by the dashboard via <c>hx-get="/Health/Panel"</c>.
/// Renders the same <see cref="HealthCheckService"/> report that backs /healthz, as HTML rows
/// instead of JSON, so the dashboard stays fully server-rendered.
/// </summary>
public sealed class PanelModel(HealthCheckService healthCheckService) : PageModel
{
    private readonly HealthCheckService healthCheckService = healthCheckService;

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["postgres"] = "PostgreSQL",
        ["ollama"] = "Ollama",
        ["tei-embed"] = "TEI Embeddings",
        ["tei-rerank"] = "TEI Reranker",
    };

    public IReadOnlyList<ServiceEntry> Entries { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var report = await this.healthCheckService.CheckHealthAsync(cancellationToken);
        this.Entries = report.Entries
            .Select(entry => new ServiceEntry(DisplayNames.GetValueOrDefault(entry.Key, entry.Key), entry.Value.Status))
            .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public sealed record ServiceEntry(string DisplayName, HealthStatus Status);
}
