using GraphRag.Config;
using GraphRag.Indexing;
using GraphRag.Indexing.Runtime;
using GraphRag.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManagedCode.GraphRag.Tests.Runtime;

public sealed class IndexingPipelineRunnerTests
{
    [Test]
    public async Task RunAsync_ExecutesPipelineAndReturnsResults()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        var capturedContexts = new List<PipelineRunContext>();
        WorkflowDelegate step = (config, context, token) =>
        {
            capturedContexts.Add(context);
            return ValueTask.FromResult(new WorkflowResult("completed"));
        };

        var pipelineFactory = new StubPipelineFactory(step);
        var executor = new PipelineExecutor(new NullLogger<PipelineExecutor>());
        var runner = new IndexingPipelineRunner(services, pipelineFactory, executor);

        var config = new GraphRagConfig
        {
            Input = new InputConfig
            {
                Storage = new StorageConfig { Type = StorageType.Memory },
                FilePattern = ".*",
                FileType = InputFileType.Text
            },
            Output = new StorageConfig { Type = StorageType.Memory },
            UpdateIndexOutput = new StorageConfig { Type = StorageType.Memory }
        };

        var results = await runner.RunAsync(config);

        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Result).IsEqualTo("completed");
        await Assert.That(capturedContexts).HasSingleItem();
        await Assert.That(capturedContexts[0].InputStorage).IsTypeOf<MemoryPipelineStorage>();
    }

    private sealed class StubPipelineFactory(WorkflowDelegate step) : IPipelineFactory
    {
        private readonly WorkflowDelegate _step = step;

        public WorkflowPipeline BuildIndexingPipeline(IndexingPipelineDescriptor descriptor)
        {
            return new WorkflowPipeline(descriptor.Name, new[] { new WorkflowStep("stub", _step) });
        }

        public WorkflowPipeline BuildQueryPipeline(QueryPipelineDescriptor descriptor)
        {
            return new WorkflowPipeline(descriptor.Name, new[] { new WorkflowStep("stub", _step) });
        }
    }
}
