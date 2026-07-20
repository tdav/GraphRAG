using GraphRag.Config;
using GraphRag.Graphs;
using GraphRag.Storage.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManagedCode.GraphRag.Tests.Storage.Postgres;

public sealed class ServiceCollectionExtensionsTests
{
    [Test]
    public async Task AddPostgresGraphStore_RegistersKeyedServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(_ => NullLoggerFactory.Instance);
        services.AddSingleton<ILogger<PostgresGraphStore>>(_ => NullLogger<PostgresGraphStore>.Instance);

        services.AddPostgresGraphStore("primary", options =>
        {
            options.ConnectionString = "Host=localhost";
            options.GraphName = "graph";
        });

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredKeyedService<PostgresGraphStore>("primary");
        await Assert.That(store).IsNotNull();
    }

    [Test]
    public async Task AddPostgresGraphStore_FirstStoreIsDefault()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(_ => NullLoggerFactory.Instance);
        services.AddSingleton<ILogger<PostgresGraphStore>>(_ => NullLogger<PostgresGraphStore>.Instance);

        services.AddPostgresGraphStore("default", options =>
        {
            options.ConnectionString = "Host=localhost";
            options.GraphName = "graph";
        });

        await using var provider = services.BuildServiceProvider();
        var keyed = provider.GetRequiredKeyedService<PostgresGraphStore>("default");
        var store = provider.GetRequiredService<PostgresGraphStore>();
        var graphStore = provider.GetRequiredService<IGraphStore>();

        await Assert.That(store).IsSameReferenceAs(keyed);
        await Assert.That(graphStore).IsSameReferenceAs(store);
    }

    [Test]
    public async Task AddPostgresGraphStore_SubsequentStoresDoNotOverrideDefault()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(_ => NullLoggerFactory.Instance);
        services.AddSingleton<ILogger<PostgresGraphStore>>(_ => NullLogger<PostgresGraphStore>.Instance);

        services.AddPostgresGraphStore("primary", options =>
        {
            options.ConnectionString = "Host=localhost";
            options.GraphName = "graph";
        });

        services.AddPostgresGraphStore("secondary", options =>
        {
            options.ConnectionString = "Host=localhost";
            options.GraphName = "graph2";
        });

        await using var provider = services.BuildServiceProvider();

        var defaultStore = provider.GetRequiredService<PostgresGraphStore>();
        var graphStore = provider.GetRequiredService<IGraphStore>();
        var primaryStore = provider.GetRequiredKeyedService<PostgresGraphStore>("primary");
        var secondaryStore = provider.GetRequiredKeyedService<PostgresGraphStore>("secondary");
        var secondaryGraphStore = provider.GetRequiredKeyedService<IGraphStore>("secondary");

        await Assert.That(defaultStore).IsSameReferenceAs(primaryStore);
        await Assert.That(graphStore).IsSameReferenceAs(defaultStore);
        await Assert.That(secondaryStore).IsNotSameReferenceAs(primaryStore);
        await Assert.That(secondaryGraphStore).IsSameReferenceAs(secondaryStore);
    }

    [Test]
    public async Task AddPostgresGraphStores_RegistersFromConfig()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(_ => NullLoggerFactory.Instance);
        services.AddSingleton<ILogger<PostgresGraphStore>>(_ => NullLogger<PostgresGraphStore>.Instance);

        var config = new GraphRagConfig();
        var graphs = config.GetPostgresGraphStores();
        graphs["primary"] = new PostgresGraphStoreConfig
        {
            ConnectionString = "Host=localhost",
            GraphName = "graph",
            AutoCreateIndexes = false,
            VertexPropertyIndexes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Person"] = new[] { "Email", "Name" }
            },
            EdgePropertyIndexes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["KNOWS"] = new[] { "Since" }
            }
        };

        services.AddPostgresGraphStores(config);

        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredKeyedService<PostgresGraphStoreOptions>("primary");
        await Assert.That(options.AutoCreateIndexes).IsFalse();
        await Assert.That(options.VertexPropertyIndexes.Keys).Contains("Person");
        await Assert.That(options.EdgePropertyIndexes.Keys).Contains("KNOWS");

        var defaultStore = provider.GetRequiredService<IGraphStore>();
        await Assert.That(defaultStore).IsNotNull();
    }
}
