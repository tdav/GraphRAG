using System.Text;
using System.Text.RegularExpressions;
using GraphRag.Storage;

namespace ManagedCode.GraphRag.Tests.Storage;

public sealed class MemoryPipelineStorageTests
{
    [Test]
    public async Task FindAsync_ReturnsMetadataFromRegexGroups()
    {
        var storage = new MemoryPipelineStorage();
        await storage.SetAsync("reports/doc-1.json", new MemoryStream(Encoding.UTF8.GetBytes("{}")));
        await storage.SetAsync("news/doc-2.json", new MemoryStream(Encoding.UTF8.GetBytes("{}")));

        var pattern = new Regex("^(?<category>[^/]+)/(?<file>.+\\.json)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var filter = new Dictionary<string, object?> { ["category"] = "news" };

        var matches = new List<PipelineStorageItem>();
        await foreach (var item in storage.FindAsync(pattern, fileFilter: filter))
        {
            matches.Add(item);
        }

        await Assert.That(matches).HasSingleItem();
        var match = matches[0];
        await Assert.That(match.Path).IsEqualTo("news/doc-2.json");
        await Assert.That(match.Metadata["category"]).IsEqualTo("news");
        await Assert.That(match.Metadata["file"]).IsEqualTo("doc-2.json");
    }

    [Test]
    public async Task GetAsync_ReturnsStoredContent()
    {
        var storage = new MemoryPipelineStorage();
        await storage.SetAsync("data/file.txt", new MemoryStream(Encoding.UTF8.GetBytes("payload")));

        await using var binaryStream = await storage.GetAsync("data/file.txt", asBytes: true);
        await Assert.That(binaryStream).IsNotNull();
        using var reader = new StreamReader(binaryStream!, Encoding.UTF8);
        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("payload");

        await using var textStream = await storage.GetAsync("data/file.txt", encoding: Encoding.UTF8);
        await Assert.That(textStream).IsNotNull();
        using var textReader = new StreamReader(textStream!, Encoding.UTF8);
        await Assert.That(await textReader.ReadToEndAsync()).IsEqualTo("payload");
    }

    [Test]
    public async Task DeleteAsync_RemovesEntries()
    {
        var storage = new MemoryPipelineStorage();
        await storage.SetAsync("item.bin", new MemoryStream([1, 2, 3]));

        await Assert.That(await storage.HasAsync("item.bin")).IsTrue();
        await storage.DeleteAsync("item.bin");
        await Assert.That(await storage.HasAsync("item.bin")).IsFalse();
    }

    [Test]
    public async Task ClearAsync_RemovesScopedEntriesOnly()
    {
        var root = new MemoryPipelineStorage();
        await root.SetAsync("root.txt", new MemoryStream(Encoding.UTF8.GetBytes("root")));

        var child = root.CreateChild("nested");
        await child.SetAsync("child.txt", new MemoryStream(Encoding.UTF8.GetBytes("child")));

        await child.ClearAsync();

        await Assert.That(await root.HasAsync("root.txt")).IsTrue();
        await Assert.That(await child.HasAsync("child.txt")).IsFalse();
    }
}
