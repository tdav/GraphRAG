namespace MyGraphRagV5.Ai;

/// <summary>
/// App-owned abstraction over a cross-encoder reranker (e.g. TEI's /rerank endpoint).
/// </summary>
public interface IReranker
{
    Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<string> texts,
        int topN,
        CancellationToken cancellationToken);
}

public record RerankResult(int Index, double Score);
