namespace MyGraphRagV5.Ai;

/// <summary>
/// Configuration for the Text Embeddings Inference (TEI) embedding and reranker services.
/// Bound from config section "Tei".
/// </summary>
public sealed record TeiOptions
{
    public string EmbedUrl { get; set; } = string.Empty;

    public string RerankUrl { get; set; } = string.Empty;

    public int EmbedBatchSize { get; set; } = 16;

    public string EmbedModelId { get; set; } = "tei-embed";
}
