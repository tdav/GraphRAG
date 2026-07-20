using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MyGraphRagV5.Ai;

/// <summary>
/// <see cref="IReranker"/> backed by a TEI (Text Embeddings Inference) server's
/// POST /rerank endpoint.
/// </summary>
public sealed class TeiReranker(HttpClient httpClient) : IReranker
{
    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<string> texts,
        int topN,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(texts);

        var response = await this.httpClient
            .PostAsJsonAsync("/rerank", new TeiRerankRequest(query, texts), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var items = await response.Content
            .ReadFromJsonAsync<List<TeiRerankResponseItem>>(cancellationToken)
            .ConfigureAwait(false)
            ?? [];

        return items
            .OrderByDescending(item => item.Score)
            .Take(topN)
            .Select(item => new RerankResult(item.Index, item.Score))
            .ToList();
    }

    private sealed record TeiRerankRequest(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("texts")] IReadOnlyList<string> Texts);

    private sealed record TeiRerankResponseItem(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("score")] double Score);
}
