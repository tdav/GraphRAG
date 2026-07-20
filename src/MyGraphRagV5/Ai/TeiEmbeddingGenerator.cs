using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MyGraphRagV5.Ai;

/// <summary>
/// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> backed by a TEI (Text Embeddings
/// Inference) server's POST /embed endpoint. Batches inputs by <see cref="TeiOptions.EmbedBatchSize"/>.
/// </summary>
public sealed class TeiEmbeddingGenerator(HttpClient httpClient, IOptions<TeiOptions> options)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly TeiOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Embedding dimension captured once from the first embedding produced, if any.</summary>
    public int? Dimension { get; private set; }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputs = values as IReadOnlyList<string> ?? values.ToList();
        var result = new GeneratedEmbeddings<Embedding<float>>(inputs.Count);

        foreach (var batch in inputs.Chunk(Math.Max(1, this.options.EmbedBatchSize)))
        {
            var response = await this.httpClient
                .PostAsJsonAsync("/embed", new TeiEmbedRequest(batch), cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var vectors = await response.Content
                .ReadFromJsonAsync<float[][]>(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("TEI /embed returned an empty response.");

            foreach (var vector in vectors)
            {
                this.Dimension ??= vector.Length;
                result.Add(new Embedding<float>(vector));
            }
        }

        return result;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }

    private sealed record TeiEmbedRequest([property: JsonPropertyName("inputs")] IReadOnlyList<string> Inputs);
}
