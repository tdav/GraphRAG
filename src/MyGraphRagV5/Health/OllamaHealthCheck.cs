using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MyGraphRagV5.Ai;

namespace MyGraphRagV5.Health;

/// <summary>
/// Checks Ollama Cloud reachability via GET {Ollama:Endpoint}/models. Reporting Unhealthy when
/// the cloud endpoint is unreachable or the key is missing is correct behavior, not a bug -
/// dev environments may not have Ollama configured.
/// </summary>
public sealed class OllamaHealthCheck(IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options) : IHealthCheck
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly IOptions<OllamaOptions> options = options;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var endpoint = this.options.Value.Endpoint.TrimEnd('/');
        try
        {
            using var client = this.httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.GetAsync($"{endpoint}/models", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"Ollama responded {(int)response.StatusCode}.")
                : HealthCheckResult.Degraded($"Ollama responded {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Ollama endpoint unreachable.", ex);
        }
    }
}
