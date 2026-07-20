using System.Text.Json;
using GraphRag;
using GraphRag.Config;
using GraphRag.Constants;
using GraphRag.Indexing;
using GraphRag.Indexing.Workflows;
using ManagedCode.GraphRag.Tests.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.GraphRag.Tests.Integration;

public sealed class IndexingPipelineRunnerTests
{
    [Test]
    public async Task RunAsync_ProcessesTextDocuments()
    {
        using var temp = new TempDirectory();
        var inputDir = Path.Combine(temp.Root, "input");
        var outputDir = Path.Combine(temp.Root, "output");
        var updateDir = Path.Combine(temp.Root, "update");

        Directory.CreateDirectory(inputDir);
        await File.WriteAllTextAsync(Path.Combine(inputDir, "doc1.txt"), "Alice met Bob at the conference.");

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IChatClient>(new TestChatClientFactory().CreateClient())
            .AddGraphRag()
            .BuildServiceProvider();

        var runner = services.GetRequiredService<IndexingPipelineRunner>();

        var config = new GraphRagConfig
        {
            Input = new InputConfig
            {
                Storage = new StorageConfig { Type = StorageType.File, BaseDir = inputDir },
                FileType = InputFileType.Text,
                FilePattern = ".*\\.txt$",
                Encoding = "utf-8"
            },
            Output = new StorageConfig { Type = StorageType.File, BaseDir = outputDir },
            UpdateIndexOutput = new StorageConfig { Type = StorageType.File, BaseDir = updateDir },
            Chunks = new ChunkingConfig
            {
                Size = 100,
                Overlap = 20,
                EncodingModel = TokenizerDefaults.DefaultEncoding
            }
        };

        var results = await runner.RunAsync(config);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results).Contains(result => result.Workflow == CreateFinalDocumentsWorkflow.Name);

        var documentsPath = Path.Combine(outputDir, PipelineTableNames.Documents + ".json");
        await Assert.That(File.Exists(documentsPath)).IsTrue();

        using var documentStream = File.OpenRead(documentsPath);
        var documents = await JsonSerializer.DeserializeAsync<JsonElement>(documentStream);
        await Assert.That(documents.ValueKind == JsonValueKind.Array).IsTrue();
        await Assert.That(documents.GetArrayLength()).IsEqualTo(1);

        var textUnitsPath = Path.Combine(outputDir, PipelineTableNames.TextUnits + ".json");
        await Assert.That(File.Exists(textUnitsPath)).IsTrue();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "GraphRag", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
