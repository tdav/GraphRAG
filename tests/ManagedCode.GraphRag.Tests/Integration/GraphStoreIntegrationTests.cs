using System.Globalization;
using GraphRag.Graphs;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.GraphRag.Tests.Integration;

[ClassDataSource<GraphRagApplicationFixture>(Shared = SharedType.PerAssembly)]
public sealed class GraphStoreIntegrationTests(GraphRagApplicationFixture fixture)
{
    public static IEnumerable<object[]> GraphProviders => GraphStoreTestProviders.ProviderKeys;

    [Test]
    [MethodDataSource(nameof(GraphProviders))]
    public async Task GraphStores_UpdateExistingNodes(string providerKey)
    {
        var store = GetStore(providerKey);
        if (store is null)
        {
            return;
        }
        await store.InitializeAsync();

        var label = GraphStoreTestProviders.GetLabel(providerKey);
        var nodeId = $"{providerKey}-update-{Guid.NewGuid():N}";

        await store.UpsertNodeAsync(nodeId, label, new Dictionary<string, object?> { ["name"] = "alpha", ["score"] = 1 });
        await store.UpsertNodeAsync(nodeId, label, new Dictionary<string, object?> { ["name"] = "beta", ["score"] = 2 });

        var node = await FindNodeAsync(store, nodeId);
        await Assert.That(node).IsNotNull();
        await Assert.That(node!.Properties["name"]).IsEqualTo("beta");
        await Assert.That(Convert.ToInt32(node.Properties["score"], CultureInfo.InvariantCulture)).IsEqualTo(2);
    }

    [Test]
    [MethodDataSource(nameof(GraphProviders))]
    public async Task GraphStores_RemovePropertiesWhenValueIsNull(string providerKey)
    {
        var store = GetStore(providerKey);
        if (store is null)
        {
            return;
        }
        await store.InitializeAsync();

        var label = GraphStoreTestProviders.GetLabel(providerKey);
        var nodeId = $"{providerKey}-cleanup-{Guid.NewGuid():N}";

        await store.UpsertNodeAsync(nodeId, label, new Dictionary<string, object?> { ["nickname"] = "alpha" });
        var node = await FindNodeAsync(store, nodeId);
        await Assert.That(node!.Properties["nickname"]).IsEqualTo("alpha");

        await store.UpsertNodeAsync(nodeId, label, new Dictionary<string, object?> { ["nickname"] = null });
        var updated = await FindNodeAsync(store, nodeId);
        await Assert.That(updated!.Properties.ContainsKey("nickname")).IsFalse();
    }

    [Test]
    [MethodDataSource(nameof(GraphProviders))]
    public async Task GraphStores_HandleBidirectionalRelationships(string providerKey)
    {
        var store = GetStore(providerKey);
        if (store is null)
        {
            return;
        }
        await store.InitializeAsync();

        var label = GraphStoreTestProviders.GetLabel(providerKey);
        var a = $"{providerKey}-bi-a-{Guid.NewGuid():N}";
        var b = $"{providerKey}-bi-b-{Guid.NewGuid():N}";

        await store.UpsertNodeAsync(a, label, new Dictionary<string, object?>());
        await store.UpsertNodeAsync(b, label, new Dictionary<string, object?>());

        var relationships = new[]
        {
            new GraphRelationshipUpsert(a, b, "KNOWS", new Dictionary<string, object?> { ["direction"] = "forward" }, Bidirectional: true)
        };

        await store.UpsertRelationshipsAsync(relationships);

        var outgoingA = await CollectAsync(store.GetOutgoingRelationshipsAsync(a));
        var outgoingB = await CollectAsync(store.GetOutgoingRelationshipsAsync(b));

        await Assert.That(outgoingA).Contains(rel => rel.TargetId == b && rel.Type == "KNOWS");
        await Assert.That(outgoingB).Contains(rel => rel.TargetId == a && rel.Type == "KNOWS");
    }

    [Test]
    [MethodDataSource(nameof(GraphProviders))]
    public async Task GraphStores_CanCreateAndRetrieveNodes(string providerKey)
    {
        var store = GetStore(providerKey);
        if (store is null)
        {
            return;
        }
        await store.InitializeAsync();

        var label = GraphStoreTestProviders.GetLabel(providerKey);
        var nodeId = $"{providerKey}-node-{Guid.NewGuid():N}";
        var props = new Dictionary<string, object?>
        {
            ["name"] = $"name-{providerKey}",
            ["index"] = 1
        };

        await store.UpsertNodeAsync(nodeId, label, props);
        var node = await FindNodeAsync(store, nodeId);
        await Assert.That(node).IsNotNull();
        await Assert.That(node!.Label).IsEqualTo(label);
        await Assert.That(node.Properties["name"]).IsEqualTo($"name-{providerKey}");
    }

    [Test]
    [MethodDataSource(nameof(GraphProviders))]
    public async Task GraphStores_CanCreateAndRetrieveRelationships(string providerKey)
    {
        var store = GetStore(providerKey);
        if (store is null)
        {
            return;
        }
        await store.InitializeAsync();

        var label = GraphStoreTestProviders.GetLabel(providerKey);
        var sourceId = $"{providerKey}-rel-src-{Guid.NewGuid():N}";
        var targetId = $"{providerKey}-rel-dst-{Guid.NewGuid():N}";

        await store.UpsertNodeAsync(sourceId, label, new Dictionary<string, object?>());
        await store.UpsertNodeAsync(targetId, label, new Dictionary<string, object?>());
        await store.UpsertRelationshipAsync(sourceId, targetId, "CONNECTS", new Dictionary<string, object?>
        {
            ["score"] = 0.99,
            ["provider"] = providerKey
        });

        var outgoing = await CollectAsync(store.GetOutgoingRelationshipsAsync(sourceId));
        await Assert.That(outgoing).HasSingleItem(rel => rel.TargetId == targetId);
        var relationship = outgoing.Single(rel => rel.TargetId == targetId);
        await Assert.That(relationship.Type).IsEqualTo("CONNECTS");
        await Assert.That(relationship.Properties["provider"]).IsEqualTo(providerKey);

        var allEdges = await CollectAsync(store.GetRelationshipsAsync());
        await Assert.That(allEdges).Contains(rel => rel.SourceId == sourceId && rel.TargetId == targetId);
    }

    [Test]
    [MethodDataSource(nameof(GraphProviders))]
    public async Task GraphStores_CanUpsertInBatch(string providerKey)
    {
        var store = GetStore(providerKey);
        if (store is null)
        {
            return;
        }
        await store.InitializeAsync();
        var label = GraphStoreTestProviders.GetLabel(providerKey);

        var nodes = Enumerable.Range(0, 3)
            .Select(index => new GraphNodeUpsert(
                $"{providerKey}-batch-node-{index}-{Guid.NewGuid():N}",
                label,
                new Dictionary<string, object?> { ["index"] = index }))
            .ToList();

        await store.UpsertNodesAsync(nodes);

        foreach (var node in nodes)
        {
            await Assert.That(await FindNodeAsync(store, node.Id)).IsNotNull();
        }

        var relationships = nodes.Skip(1)
            .Select(node => new GraphRelationshipUpsert(nodes[0].Id, node.Id, "CONNECTS", new Dictionary<string, object?>()))
            .ToList();

        await store.UpsertRelationshipsAsync(relationships);
        var outgoing = await CollectAsync(store.GetOutgoingRelationshipsAsync(nodes[0].Id));
        await Assert.That(outgoing.Count).IsEqualTo(relationships.Count);
    }

    [Test]
    [MethodDataSource(nameof(GraphProviders))]
    public async Task GraphStores_CanDeleteRelationshipsByRewriting(string providerKey)
    {
        var store = GetStore(providerKey);
        if (store is null)
        {
            return;
        }
        await store.InitializeAsync();

        var label = GraphStoreTestProviders.GetLabel(providerKey);
        var sourceId = $"{providerKey}-del-src-{Guid.NewGuid():N}";
        var targetId = $"{providerKey}-del-dst-{Guid.NewGuid():N}";

        await store.UpsertNodeAsync(sourceId, label, new Dictionary<string, object?>());
        await store.UpsertNodeAsync(targetId, label, new Dictionary<string, object?>());
        await store.UpsertRelationshipAsync(sourceId, targetId, "CONNECTS", new Dictionary<string, object?>());

        var initial = await CollectAsync(store.GetOutgoingRelationshipsAsync(sourceId));
        await Assert.That(initial).Contains(rel => rel.TargetId == targetId);

        await store.UpsertRelationshipAsync(sourceId, targetId, "CONNECTS", new Dictionary<string, object?> { ["flag"] = "active" });
        var updated = await CollectAsync(store.GetOutgoingRelationshipsAsync(sourceId));

        await Assert.That(updated).Contains(rel => rel.TargetId == targetId && rel.Properties.ContainsKey("flag"));
    }

    [Test]
    [MethodDataSource(nameof(GraphProviders))]
    public async Task GraphStores_HandlePagination(string providerKey)
    {
        var graphStore = GetStore(providerKey);
        if (graphStore is null)
        {
            return;
        }
        await graphStore.InitializeAsync();

        var label = GraphStoreTestProviders.GetLabel(providerKey);
        var nodeIds = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var id = $"{providerKey}-page-node-{i}-{Guid.NewGuid():N}";
            nodeIds.Add(id);
            await graphStore.UpsertNodeAsync(id, label, new Dictionary<string, object?> { ["index"] = i });
        }

        var firstTwo = await CollectAsync(graphStore.GetNodesAsync(new GraphTraversalOptions { Take = 2 }));
        await Assert.That(firstTwo.Count).IsEqualTo(2);

        var nextTwo = await CollectAsync(graphStore.GetNodesAsync(new GraphTraversalOptions { Skip = 2, Take = 2 }));
        await Assert.That(nextTwo.Count).IsEqualTo(2);

        await Assert.That(firstTwo.Select(n => n.Id)).IsNotEquivalentTo(nextTwo.Select(n => n.Id));

        var sourceId = nodeIds[0];
        for (var i = 1; i < nodeIds.Count; i++)
        {
            await graphStore.UpsertRelationshipAsync(sourceId, nodeIds[i], "CONNECTS", new Dictionary<string, object?> { ["index"] = i });
        }

        var pagedEdges = await CollectAsync(graphStore.GetRelationshipsAsync(new GraphTraversalOptions { Take = 2 }));
        await Assert.That(pagedEdges.Count).IsEqualTo(2);
    }

    [Test]
    [MethodDataSource(nameof(GraphProviders))]
    public async Task PostgresGraphStore_PersistsStringPropertiesWithAgtypeParameters(string providerKey)
    {
        if (!string.Equals(providerKey, "postgres", StringComparison.OrdinalIgnoreCase))
        {
            // Other providers do not use agtype parameters.
            return;
        }

        var store = GetStore(providerKey);
        await Assert.That(store).IsNotNull();
        await store!.InitializeAsync();

        var label = GraphStoreTestProviders.GetLabel(providerKey);
        var nodeId = $"{providerKey}-agtype-{Guid.NewGuid():N}";
        var payload = "line1\nline2 \"quoted\" \\ backslash and {braces}";

        await store.UpsertNodeAsync(nodeId, label, new Dictionary<string, object?>
        {
            ["content"] = payload,
            ["note"] = "ensure-agtype-parameter"
        });

        var stored = await FindNodeAsync(store, nodeId);
        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.Properties["content"]?.ToString()).IsEqualTo(payload);
    }

    [Test]
    [Property("Category", "Cosmos")]
    public async Task CosmosGraphStore_RoundTrips_WhenEmulatorAvailable()
    {
        var cosmosStore = fixture.Services.GetKeyedService<IGraphStore>("cosmos");
        if (cosmosStore is null)
        {
            return;
        }

        const string label = "Document";
        var sourceId = $"cosmos-src-{Guid.NewGuid():N}";
        var targetId = $"cosmos-dst-{Guid.NewGuid():N}";

        await cosmosStore.InitializeAsync();
        await cosmosStore.UpsertNodeAsync(sourceId, label, new Dictionary<string, object?> { ["title"] = "Source" });
        await cosmosStore.UpsertNodeAsync(targetId, label, new Dictionary<string, object?> { ["title"] = "Target" });
        await cosmosStore.UpsertRelationshipAsync(sourceId, targetId, "REFERENCES", new Dictionary<string, object?> { ["confidence"] = 0.5 });

        var relationships = new List<GraphRelationship>();
        await foreach (var edge in cosmosStore.GetOutgoingRelationshipsAsync(sourceId))
        {
            relationships.Add(edge);
        }

        await Assert.That(relationships).Contains(rel => rel.TargetId == targetId && rel.Type == "REFERENCES");

        var nodeIds = new HashSet<string>();
        await foreach (var node in cosmosStore.GetNodesAsync())
        {
            nodeIds.Add(node.Id);
        }

        await Assert.That(nodeIds).Contains(sourceId);
        await Assert.That(nodeIds).Contains(targetId);

        var edges = new List<GraphRelationship>();
        await foreach (var edge in cosmosStore.GetRelationshipsAsync())
        {
            edges.Add(edge);
        }

        await Assert.That(edges).Contains(rel => rel.SourceId == sourceId && rel.TargetId == targetId && rel.Type == "REFERENCES");
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }

    private IGraphStore? GetStore(string providerKey) =>
        fixture.Services.GetKeyedService<IGraphStore>(providerKey);

    private static async Task<GraphNode?> FindNodeAsync(IGraphStore store, string nodeId, CancellationToken cancellationToken = default)
    {
        await foreach (var node in store.GetNodesAsync(cancellationToken: cancellationToken))
        {
            if (node.Id == nodeId)
            {
                return node;
            }
        }

        return null;
    }
}
