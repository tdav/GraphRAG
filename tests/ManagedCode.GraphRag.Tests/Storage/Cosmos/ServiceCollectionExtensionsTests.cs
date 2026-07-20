using GraphRag.Graphs;
using GraphRag.Storage.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManagedCode.GraphRag.Tests.Storage.Cosmos;

public sealed class ServiceCollectionExtensionsTests
{
    private const string ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=kg==";

    [Test]
    public async Task AddCosmosGraphStore_RegistersKeyedServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<CosmosGraphStore>>(_ => NullLogger<CosmosGraphStore>.Instance);

        services.AddCosmosGraphStore("primary", options =>
        {
            options.ConnectionString = ConnectionString;
            options.DatabaseId = "graph";
            options.NodesContainerId = "nodes";
            options.EdgesContainerId = "edges";
        });

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredKeyedService<CosmosGraphStore>("primary");
        await Assert.That(store).IsNotNull();
    }

    [Test]
    public async Task AddCosmosGraphStore_FirstStoreIsDefault()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<CosmosGraphStore>>(_ => NullLogger<CosmosGraphStore>.Instance);

        services.AddCosmosGraphStore("primary", options =>
        {
            options.ConnectionString = ConnectionString;
            options.DatabaseId = "graph";
        });

        await using var provider = services.BuildServiceProvider();
        var keyed = provider.GetRequiredKeyedService<CosmosGraphStore>("primary");
        var store = provider.GetRequiredService<CosmosGraphStore>();
        var graphStore = provider.GetRequiredService<IGraphStore>();

        await Assert.That(store).IsSameReferenceAs(keyed);
        await Assert.That(graphStore).IsSameReferenceAs(store);
    }

    [Test]
    public async Task AddCosmosGraphStore_SubsequentStoresDoNotOverrideDefault()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<CosmosGraphStore>>(_ => NullLogger<CosmosGraphStore>.Instance);

        services.AddCosmosGraphStore("primary", options =>
        {
            options.ConnectionString = ConnectionString;
            options.DatabaseId = "graph";
        });

        services.AddCosmosGraphStore("secondary", options =>
        {
            options.ConnectionString = ConnectionString;
            options.DatabaseId = "graph2";
        });

        await using var provider = services.BuildServiceProvider();

        var defaultStore = provider.GetRequiredService<CosmosGraphStore>();
        var graphStore = provider.GetRequiredService<IGraphStore>();
        var primaryStore = provider.GetRequiredKeyedService<CosmosGraphStore>("primary");
        var secondaryStore = provider.GetRequiredKeyedService<CosmosGraphStore>("secondary");
        var secondaryGraphStore = provider.GetRequiredKeyedService<IGraphStore>("secondary");

        await Assert.That(defaultStore).IsSameReferenceAs(primaryStore);
        await Assert.That(graphStore).IsSameReferenceAs(defaultStore);
        await Assert.That(secondaryStore).IsNotSameReferenceAs(primaryStore);
        await Assert.That(secondaryGraphStore).IsSameReferenceAs(secondaryStore);
    }
}
