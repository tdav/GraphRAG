using GraphRag.Config;
using GraphRag.Storage;

namespace ManagedCode.GraphRag.Tests.Storage;

public sealed class PipelineStorageFactoryTests
{
    [Test]
    public async Task Create_ReturnsMemoryStorageForMemoryType()
    {
        var config = new StorageConfig { Type = StorageType.Memory };
        var storage = PipelineStorageFactory.Create(config);
        await Assert.That(storage).IsTypeOf<MemoryPipelineStorage>();
    }

    [Test]
    public async Task Create_ReturnsFileStorageForFileType()
    {
        var config = new StorageConfig { Type = StorageType.File, BaseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) };
        var storage = PipelineStorageFactory.Create(config);
        try
        {
            await Assert.That(storage).IsTypeOf<FilePipelineStorage>();
        }
        finally
        {
            if (Directory.Exists(config.BaseDir))
            {
                Directory.Delete(config.BaseDir, recursive: true);
            }
        }
    }

    [Test]
    public async Task Create_ThrowsForUnsupportedType()
    {
        var config = new StorageConfig { Type = StorageType.Blob };
        await Assert.That(() => PipelineStorageFactory.Create(config)).Throws<NotSupportedException>();
    }
}
