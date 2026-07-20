using GraphRag.Vectors;
using MyGraphRagV5.Vectors;
using Npgsql;

namespace MyGraphRagV5.Tests.Integration;

/// <summary>
/// Real-Postgres round-trip coverage for <see cref="PgVectorStore"/>, deferred from Task 3's
/// unit tests. Each test uses its own randomly named collection so tests can run in parallel
/// without colliding on the same vec_* table.
/// </summary>
[ClassDataSource<AgePgVectorFixture>(Shared = SharedType.PerAssembly)]
public sealed class PgVectorStoreIntegrationTests(AgePgVectorFixture fixture)
{
    private PgVectorStore store = null!;

    [Before(Test)]
    public void SkipIfNoDocker()
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip.Test("Docker is not available");
        }
    }

    [Before(Test)]
    public Task Setup()
    {
        if (!DockerAvailability.IsAvailable)
        {
            return Task.CompletedTask;
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(fixture.ConnectionString);
        dataSourceBuilder.UseVector();
        this.store = new PgVectorStore(dataSourceBuilder.Build());
        return Task.CompletedTask;
    }

    [After(Test)]
    public Task Cleanup()
    {
        this.store?.Dispose();
        return Task.CompletedTask;
    }

    [Test]
    public async Task SearchAsync_OrdersByCosineSimilarity_NearestFirst()
    {
        var collection = $"search_{Guid.NewGuid():N}";

        await this.store.UpsertAsync(collection, new float[] { 1, 0, 0 }, Meta("near"));
        await this.store.UpsertAsync(collection, new float[] { 0, 1, 0 }, Meta("orthogonal"));
        await this.store.UpsertAsync(collection, new float[] { -1, 0, 0 }, Meta("opposite"));

        var results = await CollectAsync(this.store.SearchAsync(collection, new float[] { 1, 0, 0 }, limit: 10));

        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Id).IsEqualTo("near");
        await Assert.That(results[1].Id).IsEqualTo("orthogonal");
        await Assert.That(results[2].Id).IsEqualTo("opposite");

        // score = 1 - cosine_distance: identical direction -> 1, orthogonal -> 0, opposite -> -1.
        await Assert.That(results[0].Score).IsCloseTo(1.0, 0.0001);
        await Assert.That(results[1].Score).IsCloseTo(0.0, 0.0001);
        await Assert.That(results[2].Score).IsCloseTo(-1.0, 0.0001);

        // Strictly decreasing (nearest first).
        await Assert.That(results[0].Score > results[1].Score).IsTrue();
        await Assert.That(results[1].Score > results[2].Score).IsTrue();

        static Dictionary<string, object?> Meta(string id) => new() { ["id"] = id, ["text"] = id };
    }

    [Test]
    public async Task SearchAsync_OnNonExistentCollection_ReturnsEmpty()
    {
        // No upsert ever happened for this collection, so its vec_* table was never created.
        // This validates the Task 3 42P01 (undefined_table) catch instead of a process-local
        // "have I ever created this table" cache.
        var collection = $"missing_{Guid.NewGuid():N}";

        var results = await CollectAsync(this.store.SearchAsync(collection, new float[] { 1, 0, 0 }, limit: 5));

        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task UpsertAsync_SameId_UpdatesInPlace()
    {
        var collection = $"upsert_{Guid.NewGuid():N}";
        var metadata = new Dictionary<string, object?> { ["id"] = "fixed-id", ["text"] = "first" };

        await this.store.UpsertAsync(collection, new float[] { 1, 0, 0 }, metadata);
        await this.store.UpsertAsync(
            collection,
            new float[] { 0, 1, 0 },
            new Dictionary<string, object?> { ["id"] = "fixed-id", ["text"] = "second" });

        var results = await CollectAsync(this.store.SearchAsync(collection, new float[] { 0, 1, 0 }, limit: 10));

        await Assert.That(results).HasSingleItem();
        var single = results.Single();
        await Assert.That(single.Id).IsEqualTo("fixed-id");
        await Assert.That(single.Metadata["text"]).IsEqualTo("second");
        await Assert.That(single.Score).IsCloseTo(1.0, 0.0001);
    }

    [Test]
    public async Task Metadata_RoundTrips_ToNativeClrTypes()
    {
        var collection = $"meta_{Guid.NewGuid():N}";
        var metadata = new Dictionary<string, object?>
        {
            ["id"] = "typed",
            ["text"] = "typed row",
            ["count"] = 42,
            ["ratio"] = 3.5,
            ["active"] = true,
        };

        await this.store.UpsertAsync(collection, new float[] { 1, 0, 0 }, metadata);

        var results = await CollectAsync(this.store.SearchAsync(collection, new float[] { 1, 0, 0 }, limit: 1));
        await Assert.That(results).HasSingleItem();
        var result = results.Single();

        // jsonb round-trips integral numbers as Int64 (JSON has no int/long distinction) and
        // PgVectorNaming.JsonElementToClr picks Int64 for integral values - the point of this
        // assertion is that it comes back as a native numeric/bool CLR value, not a JsonElement.
        await Assert.That(result.Metadata["count"]).IsTypeOf<long>();
        await Assert.That(result.Metadata["count"]).IsEqualTo(42L);
        await Assert.That(result.Metadata["ratio"]).IsTypeOf<double>();
        await Assert.That(result.Metadata["ratio"]).IsEqualTo(3.5);
        await Assert.That(result.Metadata["active"]).IsTypeOf<bool>();
        await Assert.That((bool)result.Metadata["active"]!).IsTrue();
        await Assert.That(result.Metadata["text"]).IsTypeOf<string>();
    }

    private static async Task<List<VectorSearchResult>> CollectAsync(IAsyncEnumerable<VectorSearchResult> source)
    {
        var list = new List<VectorSearchResult>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }
}
