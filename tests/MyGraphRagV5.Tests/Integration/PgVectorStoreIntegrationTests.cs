using GraphRag.Vectors;
using MyGraphRagV5.Vectors;
using Npgsql;

namespace MyGraphRagV5.Tests.Integration;

/// <summary>
/// Real-Postgres round-trip coverage for <see cref="PgVectorStore"/>, deferred from Task 3's
/// unit tests. Each test uses its own randomly named collection so tests can run in parallel
/// without colliding on the same vec_* table.
/// </summary>
[Collection(AgePgVectorCollection.Name)]
public sealed class PgVectorStoreIntegrationTests : IAsyncLifetime
{
    private readonly AgePgVectorFixture fixture;
    private PgVectorStore store = null!;

    public PgVectorStoreIntegrationTests(AgePgVectorFixture fixture)
    {
        this.fixture = fixture;
    }

    public Task InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable)
        {
            return Task.CompletedTask;
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(this.fixture.ConnectionString);
        dataSourceBuilder.UseVector();
        this.store = new PgVectorStore(dataSourceBuilder.Build());
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        this.store?.Dispose();
        return Task.CompletedTask;
    }

    [DockerAvailableFact]
    public async Task SearchAsync_OrdersByCosineSimilarity_NearestFirst()
    {
        var collection = $"search_{Guid.NewGuid():N}";

        await this.store.UpsertAsync(collection, new float[] { 1, 0, 0 }, Meta("near"));
        await this.store.UpsertAsync(collection, new float[] { 0, 1, 0 }, Meta("orthogonal"));
        await this.store.UpsertAsync(collection, new float[] { -1, 0, 0 }, Meta("opposite"));

        var results = await CollectAsync(this.store.SearchAsync(collection, new float[] { 1, 0, 0 }, limit: 10));

        Assert.Equal(3, results.Count);
        Assert.Equal("near", results[0].Id);
        Assert.Equal("orthogonal", results[1].Id);
        Assert.Equal("opposite", results[2].Id);

        // score = 1 - cosine_distance: identical direction -> 1, orthogonal -> 0, opposite -> -1.
        Assert.Equal(1.0, results[0].Score, precision: 4);
        Assert.Equal(0.0, results[1].Score, precision: 4);
        Assert.Equal(-1.0, results[2].Score, precision: 4);

        // Strictly decreasing (nearest first).
        Assert.True(results[0].Score > results[1].Score);
        Assert.True(results[1].Score > results[2].Score);

        static Dictionary<string, object?> Meta(string id) => new() { ["id"] = id, ["text"] = id };
    }

    [DockerAvailableFact]
    public async Task SearchAsync_OnNonExistentCollection_ReturnsEmpty()
    {
        // No upsert ever happened for this collection, so its vec_* table was never created.
        // This validates the Task 3 42P01 (undefined_table) catch instead of a process-local
        // "have I ever created this table" cache.
        var collection = $"missing_{Guid.NewGuid():N}";

        var results = await CollectAsync(this.store.SearchAsync(collection, new float[] { 1, 0, 0 }, limit: 5));

        Assert.Empty(results);
    }

    [DockerAvailableFact]
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

        var single = Assert.Single(results);
        Assert.Equal("fixed-id", single.Id);
        Assert.Equal("second", single.Metadata["text"]);
        Assert.Equal(1.0, single.Score, precision: 4);
    }

    [DockerAvailableFact]
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
        var result = Assert.Single(results);

        // jsonb round-trips integral numbers as Int64 (JSON has no int/long distinction) and
        // PgVectorNaming.JsonElementToClr picks Int64 for integral values - the point of this
        // assertion is that it comes back as a native numeric/bool CLR value, not a JsonElement.
        Assert.IsType<long>(result.Metadata["count"]);
        Assert.Equal(42L, result.Metadata["count"]);
        Assert.IsType<double>(result.Metadata["ratio"]);
        Assert.Equal(3.5, result.Metadata["ratio"]);
        Assert.IsType<bool>(result.Metadata["active"]);
        Assert.Equal(true, result.Metadata["active"]);
        Assert.IsType<string>(result.Metadata["text"]);
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
