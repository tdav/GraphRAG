using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MyGraphRagV5.Ai;

namespace MyGraphRagV5.Health;

/// <summary>
/// Checks Ollama Cloud reachability via GET {Ollama:Endpoint}/models. When an API key is
/// configured, the request is sent with it (Authorization: Bearer) so the check validates the
/// key itself rather than just endpoint reachability. Reporting Unhealthy when the cloud
/// endpoint is unreachable or the key is missing/invalid is correct behavior, not a bug - dev
/// environments may not have Ollama configured.
/// </summary>
public sealed class OllamaHealthCheck(IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options) : IHealthCheck
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly IOptions<OllamaOptions> options = options;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var ollamaOptions = this.options.Value;
        var endpoint = ollamaOptions.Endpoint.TrimEnd('/');
        var apiKey = ollamaOptions.ApiKey;
        try
        {
            using var client = this.httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}/models");
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Ollama responded {(int)response.StatusCode}.");
            }

            if (!string.IsNullOrEmpty(apiKey) && response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return HealthCheckResult.Unhealthy("Ollama rejected the API key.");
            }

            return string.IsNullOrEmpty(apiKey)
                ? HealthCheckResult.Degraded($"Ollama responded {(int)response.StatusCode}.")
                : HealthCheckResult.Unhealthy($"Ollama responded {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Ollama endpoint unreachable.", ex);
        }
    }
}
