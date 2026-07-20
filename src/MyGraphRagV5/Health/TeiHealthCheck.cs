using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MyGraphRagV5.Ai;

namespace MyGraphRagV5.Health;

/// <summary>Which TEI service a <see cref="TeiHealthCheck"/> instance targets.</summary>
public enum TeiEndpointKind
{
    Embed,
    Rerank,
}

/// <summary>
/// Checks a TEI (Text Embeddings Inference) service's GET /health endpoint. One class serves
/// both the embedding and reranker services (registered twice via AddTypeActivatedCheck with
/// different <see cref="TeiEndpointKind"/> args) instead of two near-duplicate classes.
/// Reporting Unhealthy when the container is offline is correct in dev.
/// </summary>
public sealed class TeiHealthCheck(IHttpClientFactory httpClientFactory, IOptions<TeiOptions> options, TeiEndpointKind kind) : IHealthCheck
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly IOptions<TeiOptions> options = options;
    private readonly TeiEndpointKind kind = kind;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var baseUrl = (this.kind == TeiEndpointKind.Embed ? this.options.Value.EmbedUrl : this.options.Value.RerankUrl).TrimEnd('/');
        try
        {
            using var client = this.httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.GetAsync($"{baseUrl}/health", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"TEI {this.kind} responded {(int)response.StatusCode}.")
                : HealthCheckResult.Degraded($"TEI {this.kind} responded {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"TEI {this.kind} service unreachable.", ex);
        }
    }
}
