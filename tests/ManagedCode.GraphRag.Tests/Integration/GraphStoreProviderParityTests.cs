using System.Globalization;
using GraphRag.Graphs;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.GraphRag.Tests.Integration;

[ClassDataSource<GraphRagApplicationFixture>(Shared = SharedType.PerAssembly)]
public sealed class GraphStoreProviderParityTests(GraphRagApplicationFixture fixture)
{
    public static IEnumerable<object[]> Providers => GraphStoreTestProviders.ProviderKeysAndLabels;

    [Test]
    [MethodDataSource(nameof(Providers))]
    public async Task GraphStores_ExecuteFullCrudFlows(string providerKey, string label)
    {
        var store = fixture.Services.GetKeyedService<IGraphStore>(providerKey);
        if (store is null)
        {
            return;
        }

        await store.InitializeAsync();
        var prefix = $"{providerKey}-flow-{Guid.NewGuid():N}";
        var rootId = $"{prefix}-root";
        var neighborId = $"{prefix}-neighbor";

        await store.UpsertNodeAsync(rootId, label, new Dictionary<string, object?> { ["name"] = "root", ["count"] = 1 });
        await store.UpsertNodeAsync(neighborId, label, new Dictionary<string, object?> { ["name"] = "neighbor", ["count"] = 2 });

        await store.UpsertNodeAsync(rootId, label, new Dictionary<string, object?> { ["name"] = null, ["count"] = 3, ["updated"] = true });
        var rootNode = await FindNodeAsync(store, rootId);
        await Assert.That(rootNode).IsNotNull();
        await Assert.That(Convert.ToInt32(rootNode!.Properties["count"], CultureInfo.InvariantCulture)).IsEqualTo(3);
        await Assert.That(rootNode.Properties.ContainsKey("name")).IsFalse();

        var batchedNodes = Enumerable.Range(0, 5)
            .Select(index => new GraphNodeUpsert(
                $"{prefix}-extra-{index:D2}",
                label,
                new Dictionary<string, object?> { ["index"] = index, ["origin"] = providerKey }))
            .ToList();

        await store.UpsertNodesAsync(batchedNodes);
        foreach (var node in batchedNodes)
        {
            await Assert.That(await FindNodeAsync(store, node.Id)).IsNotNull();
        }

        var relationships = new List<GraphRelationshipUpsert>
        {
            new(rootId, neighborId, "LINKS", new Dictionary<string, object?> { ["weight"] = 0.5 }),
            new(neighborId, rootId, "LINKS", new Dictionary<string, object?> { ["weight"] = 0.6 }, Bidirectional: true)
        };

        await store.UpsertRelationshipsAsync(relationships);
        var outgoing = await CollectAsync(store.GetOutgoingRelationshipsAsync(rootId));
        await Assert.That(outgoing).Contains(rel => rel.TargetId == neighborId && rel.Type == "LINKS");

        var additionalKeys = outgoing
            .Where(rel => rel.TargetId.StartsWith(prefix, StringComparison.Ordinal))
            .Select(rel => new GraphRelationshipKey(rel.SourceId, rel.TargetId, rel.Type))
            .ToList();
        if (additionalKeys.Count > 0)
        {
            await store.DeleteRelationshipsAsync(additionalKeys);
        }

        var relationshipCheck = await CollectAsync(store.GetOutgoingRelationshipsAsync(rootId));
        foreach (var key in additionalKeys)
        {
            await Assert.That(relationshipCheck)
                .DoesNotContain(rel => rel.SourceId == key.SourceId && rel.TargetId == key.TargetId && rel.Type == key.Type);
        }

        var nodesToDelete = batchedNodes.Select(node => node.Id).Append(neighborId).ToList();
        if (nodesToDelete.Count > 0)
        {
            await store.DeleteNodesAsync(nodesToDelete);
        }

        foreach (var nodeId in nodesToDelete)
        {
            await Assert.That(await FindNodeAsync(store, nodeId)).IsNull();
        }
    }

    [Test]
    [MethodDataSource(nameof(Providers))]
    public async Task GraphStores_HandleNodeInjectionPayloads(string providerKey, string label)
    {
        var store = fixture.Services.GetKeyedService<IGraphStore>(providerKey);
        if (store is null)
        {
            return;
        }

        await store.InitializeAsync();
        var sentinelId = $"{providerKey}-sentinel-{Guid.NewGuid():N}";
        await store.UpsertNodeAsync(sentinelId, label, new Dictionary<string, object?> { ["role"] = "sentinel" });

        var payload = "\" }) MATCH (victim) DETACH DELETE victim //";
        var nodeId = $"{providerKey}-inject-{Guid.NewGuid():N}";
        await store.UpsertNodeAsync(nodeId, label, new Dictionary<string, object?> { ["bio"] = payload });

        var stored = await FindNodeAsync(store, nodeId);
        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.Properties["bio"]?.ToString()).IsEqualTo(payload);

        var sentinel = await FindNodeAsync(store, sentinelId);
        await Assert.That(sentinel).IsNotNull();
    }

    [Test]
    [MethodDataSource(nameof(Providers))]
    public async Task GraphStores_HandleRelationshipInjectionPayloads(string providerKey, string label)
    {
        var store = fixture.Services.GetKeyedService<IGraphStore>(providerKey);
        if (store is null)
        {
            return;
        }

        await store.InitializeAsync();
        var sourceId = $"{providerKey}-inj-src-{Guid.NewGuid():N}";
        var targetId = $"{providerKey}-inj-dst-{Guid.NewGuid():N}";

        await store.UpsertNodeAsync(sourceId, label, new Dictionary<string, object?>());
        await store.UpsertNodeAsync(targetId, label, new Dictionary<string, object?>());

        var maliciousWeight = "0.99 }) MATCH (m) DETACH DELETE m //";
        await store.UpsertRelationshipAsync(sourceId, targetId, "TRANSFERRED", new Dictionary<string, object?> { ["weight"] = maliciousWeight, ["flag"] = false });

        var relationships = await CollectAsync(store.GetOutgoingRelationshipsAsync(sourceId));
        await Assert.That(relationships).HasSingleItem(rel => rel.TargetId == targetId);
        var stored = relationships.Single(rel => rel.TargetId == targetId);
        await Assert.That(stored.Properties["weight"]?.ToString()).IsEqualTo(maliciousWeight);
        await Assert.That(Convert.ToBoolean(stored.Properties["flag"], CultureInfo.InvariantCulture)).IsFalse();

        var targetNode = await FindNodeAsync(store, targetId);
        await Assert.That(targetNode).IsNotNull();
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var results = new List<T>();
        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }

    private static async Task<GraphNode?> FindNodeAsync(IGraphStore store, string nodeId)
    {
        await foreach (var node in store.GetNodesAsync())
        {
            if (node.Id == nodeId)
            {
                return node;
            }
        }

        return null;
    }
}
