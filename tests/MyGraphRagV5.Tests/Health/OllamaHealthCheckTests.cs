using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MyGraphRagV5.Ai;
using MyGraphRagV5.Health;
using MyGraphRagV5.Tests.Ai;

namespace MyGraphRagV5.Tests.Health;

public class OllamaHealthCheckTests
{
    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private static OllamaHealthCheck CreateHealthCheck(StubHttpMessageHandler handler, string? apiKey)
    {
        var client = new HttpClient(handler);
        var options = Options.Create(new OllamaOptions { Endpoint = "http://ollama.test", ApiKey = apiKey });
        return new OllamaHealthCheck(new FakeHttpClientFactory(client), options);
    }

    [Test]
    public async Task CheckHealthAsync_WithApiKey_SendsBearerAuthorizationAndReportsHealthyOn200()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var healthCheck = CreateHealthCheck(handler, "secret-key");

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        await Assert.That(handler.Requests).HasSingleItem();
        var request = handler.Requests.Single();
        await Assert.That(request.Request.Headers.Authorization?.Scheme).IsEqualTo("Bearer");
        await Assert.That(request.Request.Headers.Authorization?.Parameter).IsEqualTo("secret-key");
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
    }

    [Test]
    public async Task CheckHealthAsync_WithApiKey_ReportsUnhealthyOn401()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var healthCheck = CreateHealthCheck(handler, "secret-key");

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).IsEqualTo("Ollama rejected the API key.");
    }

    [Test]
    public async Task CheckHealthAsync_WithoutApiKey_SendsNoAuthorizationAndReportsHealthyOn200()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var healthCheck = CreateHealthCheck(handler, apiKey: null);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        await Assert.That(handler.Requests).HasSingleItem();
        var request = handler.Requests.Single();
        await Assert.That(request.Request.Headers.Authorization).IsNull();
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
    }
}
