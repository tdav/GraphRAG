using System.Globalization;
using GraphRag.Graphs;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.GraphRag.Tests.Integration;

[ClassDataSource<GraphRagApplicationFixture>(Shared = SharedType.PerAssembly)]
public sealed class JanusGraphStoreTests(GraphRagApplicationFixture fixture)
{
    private readonly GraphRagApplicationFixture _fixture = fixture;

    [Test]
    public async Task JanusGraphStore_UpsertsNodesAndRelationships()
    {
        var store = _fixture.Services.GetKeyedService<IGraphStore>("janus");
        if (store is null)
        {
            return;
        }
        await store.InitializeAsync();

        var aliceId = $"janus-alice-{Guid.NewGuid():N}";
        var bobId = $"janus-bob-{Guid.NewGuid():N}";

        await store.UpsertNodeAsync(aliceId, "Person", new Dictionary<string, object?> { ["name"] = "Alice" });
        await store.UpsertNodeAsync(bobId, "Person", new Dictionary<string, object?> { ["name"] = "Bob" });
        await store.UpsertRelationshipAsync(aliceId, bobId, "KNOWS", new Dictionary<string, object?> { ["since"] = 2024 });

        var relationships = new List<GraphRelationship>();
        await foreach (var relationship in store.GetOutgoingRelationshipsAsync(aliceId))
        {
            relationships.Add(relationship);
        }

        await Assert.That(relationships).HasSingleItem(r => r.TargetId == bobId);
        var stored = relationships.Single(r => r.TargetId == bobId);
        await Assert.That(stored.Type).IsEqualTo("KNOWS");
        await Assert.That(Convert.ToInt32(stored.Properties["since"], CultureInfo.InvariantCulture)).IsEqualTo(2024);
    }

    [Test]
    public async Task JanusGraphStore_ReturnsNodes()
    {
        var store = _fixture.Services.GetKeyedService<IGraphStore>("janus");
        if (store is null)
        {
            return;
        }
        await store.InitializeAsync();

        var id = $"janus-node-{Guid.NewGuid():N}";
        await store.UpsertNodeAsync(id, "Document", new Dictionary<string, object?> { ["title"] = "GraphRecord" });

        var nodes = new List<GraphNode>();
        await foreach (var node in store.GetNodesAsync())
        {
            nodes.Add(node);
        }

        await Assert.That(nodes).Contains(node => node.Id == id && node.Properties["title"]?.ToString() == "GraphRecord");
    }

    [Test]
    public async Task JanusGraphStore_ReturnsRelationships()
    {
        var store = _fixture.Services.GetKeyedService<IGraphStore>("janus");
        if (store is null)
        {
            return;
        }
        await store.InitializeAsync();

        var src = $"janus-src-{Guid.NewGuid():N}";
        var dst = $"janus-dst-{Guid.NewGuid():N}";

        await store.UpsertNodeAsync(src, "Topic", new Dictionary<string, object?>());
        await store.UpsertNodeAsync(dst, "Topic", new Dictionary<string, object?>());
        await store.UpsertRelationshipAsync(src, dst, "LINKS_TO", new Dictionary<string, object?> { ["weight"] = 0.75 });

        var relationships = new List<GraphRelationship>();
        await foreach (var relationship in store.GetRelationshipsAsync())
        {
            relationships.Add(relationship);
        }

        await Assert.That(relationships)
            .Contains(rel => rel.SourceId == src && rel.TargetId == dst && rel.Type == "LINKS_TO" &&
                              Math.Abs(Convert.ToDouble(rel.Properties["weight"], CultureInfo.InvariantCulture) - 0.75) < 0.001);
    }
}
