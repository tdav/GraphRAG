using GraphRag.Graphs;

namespace MyGraphRagV5.Query;

/// <summary>
/// Seam over <see cref="MyGraphRagV5.Indexing.ProjectGraphStoreProvider"/> so consumers like
/// <see cref="RagQueryService"/> can be unit-tested without a live Postgres/AGE connection.
/// </summary>
public interface IProjectGraphStoreProvider
{
    Task<IGraphStore> GetStoreAsync(string graphName, CancellationToken cancellationToken = default);
}
