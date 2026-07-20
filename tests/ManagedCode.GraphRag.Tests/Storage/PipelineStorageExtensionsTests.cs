using GraphRag.Storage;

namespace ManagedCode.GraphRag.Tests.Storage;

public sealed class PipelineStorageExtensionsTests
{
    [Test]
    public async Task WriteAndLoadTable_RoundTripsRecords()
    {
        var storage = new MemoryPipelineStorage();
        var records = new[] { new SampleRecord { Id = 1, Name = "Alice" } };

        await storage.WriteTableAsync("records", records);
        await Assert.That(await storage.TableExistsAsync("records")).IsTrue();

        var loaded = await storage.LoadTableAsync<SampleRecord>("records");

        await Assert.That(loaded).HasSingleItem();
        await Assert.That(loaded[0].Id).IsEqualTo(1);
        await Assert.That(loaded[0].Name).IsEqualTo("Alice");
    }

    [Test]
    public async Task DeleteTableAsync_RemovesStoredData()
    {
        var storage = new MemoryPipelineStorage();
        await storage.WriteTableAsync("records", new[] { new SampleRecord { Id = 2, Name = "Bob" } });

        await storage.DeleteTableAsync("records");

        await Assert.That(await storage.TableExistsAsync("records")).IsFalse();
    }

    [Test]
    public async Task LoadTableAsync_ThrowsWhenMissing()
    {
        var storage = new MemoryPipelineStorage();
        var exception = await Assert.That(async () => { await storage.LoadTableAsync<SampleRecord>("missing"); }).Throws<FileNotFoundException>();
        await Assert.That(exception!.Message).Contains("missing");
    }

    private sealed record SampleRecord
    {
        public int Id { get; init; }
        public string? Name { get; init; }
    }
}
