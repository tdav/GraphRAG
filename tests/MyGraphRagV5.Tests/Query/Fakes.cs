using System.Runtime.CompilerServices;
using GraphRag.Graphs;
using GraphRag.Vectors;
using Microsoft.Extensions.AI;
using MyGraphRagV5.Ai;
using MyGraphRagV5.Query;

namespace MyGraphRagV5.Tests.Query;

internal sealed class FakeVectorStore : IVectorStore
{
    public List<VectorSearchResult> Results { get; } = [];

    public string? LastCollection { get; private set; }

    public int? LastLimit { get; private set; }

    public Task UpsertAsync(
        string collection,
        ReadOnlyMemory<float> embedding,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by RagQueryService.");

    public async IAsyncEnumerable<VectorSearchResult> SearchAsync(
        string collection,
        ReadOnlyMemory<float> embedding,
        int limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.LastCollection = collection;
        this.LastLimit = limit;
        await Task.Yield();
        foreach (var result in this.Results)
        {
            yield return result;
        }
    }
}

internal sealed class FakeGraphStore : IGraphStore
{
    public Dictionary<string, List<GraphRelationship>> OutgoingBySource { get; } = new(StringComparer.Ordinal);

    public List<string> RequestedSourceIds { get; } = [];

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpsertNodeAsync(string id, string label, IReadOnlyDictionary<string, object?> properties, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UpsertNodesAsync(IReadOnlyCollection<GraphNodeUpsert> nodes, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UpsertRelationshipAsync(string sourceId, string targetId, string type, IReadOnlyDictionary<string, object?> properties, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UpsertRelationshipsAsync(IReadOnlyCollection<GraphRelationshipUpsert> relationships, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteNodesAsync(IReadOnlyCollection<string> nodeIds, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteRelationshipsAsync(IReadOnlyCollection<GraphRelationshipKey> relationships, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public async IAsyncEnumerable<GraphRelationship> GetOutgoingRelationshipsAsync(
        string sourceId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.RequestedSourceIds.Add(sourceId);
        await Task.Yield();
        if (this.OutgoingBySource.TryGetValue(sourceId, out var relationships))
        {
            foreach (var relationship in relationships)
            {
                yield return relationship;
            }
        }
    }

    public IAsyncEnumerable<GraphNode> GetNodesAsync(GraphTraversalOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<GraphRelationship> GetRelationshipsAsync(GraphTraversalOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class FakeProjectGraphStoreProvider(IGraphStore store) : IProjectGraphStoreProvider
{
    public string? LastGraphName { get; private set; }

    public Task<IGraphStore> GetStoreAsync(string graphName, CancellationToken cancellationToken = default)
    {
        this.LastGraphName = graphName;
        return Task.FromResult(store);
    }
}

internal sealed class FakeReranker(IReadOnlyList<RerankResult> results) : IReranker
{
    public string? LastQuery { get; private set; }

    public IReadOnlyList<string>? LastTexts { get; private set; }

    public int? LastTopN { get; private set; }

    public Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<string> texts,
        int topN,
        CancellationToken cancellationToken)
    {
        this.LastQuery = query;
        this.LastTexts = texts;
        this.LastTopN = topN;
        return Task.FromResult(results);
    }
}

internal sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var count = values.Count();
        var result = new GeneratedEmbeddings<Embedding<float>>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f }));
        }

        return Task.FromResult(result);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}

internal sealed class FakeChatClient : IChatClient
{
    public FakeChatClient(string answer, IReadOnlyList<string>? streamChunks = null)
    {
        this.Answer = answer;
        this.StreamChunks = streamChunks ?? [answer];
    }

    public string Answer { get; }

    public IReadOnlyList<string> StreamChunks { get; }

    public IEnumerable<ChatMessage>? LastMessages { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        this.LastMessages = messages.ToList();
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, this.Answer)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.LastMessages = messages.ToList();
        await Task.Yield();
        foreach (var chunk in this.StreamChunks)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
