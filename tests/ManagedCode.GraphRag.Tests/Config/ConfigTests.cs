using GraphRag.Config;

namespace ManagedCode.GraphRag.Tests.Config;

public sealed class ConfigTests
{
    [Test]
    public async Task StorageConfig_AllowsCustomValues()
    {
        var config = new StorageConfig
        {
            Type = StorageType.Memory,
            BaseDir = "data",
            ConnectionString = "conn",
            ContainerName = "container",
            StorageAccountBlobUrl = "https://example.com",
            CosmosDbAccountUrl = "https://cosmos.com"
        };

        await Assert.That(config.Type).IsEqualTo(StorageType.Memory);
        await Assert.That(config.BaseDir).IsEqualTo("data");
        await Assert.That(config.ConnectionString).IsEqualTo("conn");
        await Assert.That(config.ContainerName).IsEqualTo("container");
        await Assert.That(config.StorageAccountBlobUrl).IsEqualTo("https://example.com");
        await Assert.That(config.CosmosDbAccountUrl).IsEqualTo("https://cosmos.com");
    }

    [Test]
    public async Task ReportingConfig_AllowsCustomValues()
    {
        var config = new ReportingConfig
        {
            Type = ReportingType.Blob,
            BaseDir = "reports",
            ConnectionString = "conn",
            ContainerName = "container",
            StorageAccountBlobUrl = "https://blob"
        };

        await Assert.That(config.Type).IsEqualTo(ReportingType.Blob);
        await Assert.That(config.BaseDir).IsEqualTo("reports");
        await Assert.That(config.ConnectionString).IsEqualTo("conn");
    }

    [Test]
    public async Task SnapshotsConfig_StoresFlags()
    {
        var config = new SnapshotsConfig
        {
            Embeddings = true,
            GraphMl = true,
            RawGraph = false
        };

        await Assert.That(config.Embeddings).IsTrue();
        await Assert.That(config.GraphMl).IsTrue();
        await Assert.That(config.RawGraph).IsFalse();
    }

    [Test]
    public async Task VectorStoreSchemaConfig_AllowsCustomization()
    {
        var config = new VectorStoreSchemaConfig
        {
            IdField = "id_field",
            VectorField = "vec",
            TextField = "text_field",
            AttributesField = "attrs",
            VectorSize = 42,
            IndexName = "index"
        };

        await Assert.That(config.IdField).IsEqualTo("id_field");
        await Assert.That(config.VectorField).IsEqualTo("vec");
        await Assert.That(config.VectorSize).IsEqualTo(42);
        await Assert.That(config.IndexName).IsEqualTo("index");
    }

    [Test]
    public async Task GraphRagConfig_InitializesEmptyModelSet()
    {
        var config = new GraphRagConfig();

        await Assert.That(config.Models).IsEmpty();
    }
}
