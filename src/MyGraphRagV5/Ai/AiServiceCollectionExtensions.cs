using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;

namespace MyGraphRagV5.Ai;

/// <summary>
/// Registers the Ollama chat client, TEI embedding generator and TEI reranker.
/// The GraphRag library resolves <see cref="IChatClient"/> and
/// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> via keyed DI (by model id) with
/// unkeyed fallback, so both a keyed and an unkeyed registration are required.
/// </summary>
public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddAiClients(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        services.Configure<OllamaOptions>(config.GetSection("Ollama"));
        services.Configure<TeiOptions>(config.GetSection("Tei"));

        // Model ids must be known synchronously to use as keyed-DI keys; reading them from
        // config here does not construct the actual clients (that stays lazy, see below).
        var ollamaModelId = config["Ollama:Model"] is { Length: > 0 } model ? model : new OllamaOptions().Model;
        var embedModelId = config["Tei:EmbedModelId"] is { Length: > 0 } embedModel ? embedModel : new TeiOptions().EmbedModelId;

        services.AddKeyedSingleton<IChatClient>(ollamaModelId, (sp, _) =>
            CreateChatClient(sp.GetRequiredService<IOptions<OllamaOptions>>().Value));
        services.AddSingleton<IChatClient>(sp =>
            sp.GetRequiredKeyedService<IChatClient>(ollamaModelId));

        services.AddHttpClient("tei-embed", (sp, client) =>
            client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<TeiOptions>>().Value.EmbedUrl));
        services.AddHttpClient("tei-rerank", (sp, client) =>
            client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<TeiOptions>>().Value.RerankUrl));

        services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embedModelId, (sp, _) =>
            new TeiEmbeddingGenerator(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("tei-embed"),
                sp.GetRequiredService<IOptions<TeiOptions>>()));
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            sp.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>(embedModelId));

        services.AddSingleton<IReranker>(sp =>
            new TeiReranker(sp.GetRequiredService<IHttpClientFactory>().CreateClient("tei-rerank")));

        return services;
    }

    /// <summary>
    /// Placeholder used when no API key is configured. <see cref="ApiKeyCredential"/> rejects an
    /// empty string at construction time, which would fail DI resolution of <see cref="IChatClient"/>
    /// before any HTTP call is made. Using a non-empty placeholder defers the failure to the first
    /// actual request, which then fails as a 401 from the server.
    /// </summary>
    private const string MissingApiKeyPlaceholder = "unset";

    private static IChatClient CreateChatClient(OllamaOptions options)
    {
        var credential = new ApiKeyCredential(options.ApiKey ?? MissingApiKeyPlaceholder);
        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) };
        var openAiClient = new OpenAIClient(credential, clientOptions);
        return openAiClient.GetChatClient(options.Model).AsIChatClient();
    }
}
