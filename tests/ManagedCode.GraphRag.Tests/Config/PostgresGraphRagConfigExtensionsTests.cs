using GraphRag.Config;
using GraphRag.Storage.Postgres;

namespace ManagedCode.GraphRag.Tests.Config;

public sealed class PostgresGraphRagConfigExtensionsTests
{
    [Test]
    public async Task GetPostgresGraphStores_ReturnsMutableDictionary()
    {
        var config = new GraphRagConfig();
        var stores = config.GetPostgresGraphStores();
        await Assert.That(stores).IsEmpty();

        stores["primary"] = new PostgresGraphStoreConfig { ConnectionString = "Host=localhost", GraphName = "g" };

        var replay = config.GetPostgresGraphStores();
        await Assert.That(replay["primary"].GraphName).IsEqualTo("g");
    }

    [Test]
    public async Task SetPostgresGraphStores_ReplacesExisting()
    {
        var config = new GraphRagConfig();
        var initial = new Dictionary<string, PostgresGraphStoreConfig>
        {
            ["default"] = new() { ConnectionString = "conn", GraphName = "g" }
        };

        config.SetPostgresGraphStores(initial);
        var stores = config.GetPostgresGraphStores();
        await Assert.That(stores["default"].GraphName).IsEqualTo("g");

        config.SetPostgresGraphStores(null!);
        await Assert.That(config.GetPostgresGraphStores()).IsEmpty();
    }
}
