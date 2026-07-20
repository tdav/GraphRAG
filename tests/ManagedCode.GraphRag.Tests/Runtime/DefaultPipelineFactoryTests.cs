using GraphRag.Indexing.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.GraphRag.Tests.Runtime;

public sealed class DefaultPipelineFactoryTests
{
    [Test]
    public async Task BuildIndexingPipeline_ResolvesKeyedDelegates()
    {
        WorkflowDelegate first = (config, context, token) => ValueTask.FromResult(new WorkflowResult("first"));
        WorkflowDelegate second = (config, context, token) => ValueTask.FromResult(new WorkflowResult("second"));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<WorkflowDelegate>("one", (_, _) => first);
        services.AddKeyedSingleton<WorkflowDelegate>("two", (_, _) => second);

        using var provider = services.BuildServiceProvider();
        var factory = new DefaultPipelineFactory(provider);

        var descriptor = new IndexingPipelineDescriptor("pipeline", new[] { "one", "two" });
        var pipeline = factory.BuildIndexingPipeline(descriptor);

        await Assert.That(pipeline.Name).IsEqualTo("pipeline");
        await Assert.That(pipeline.Names).IsEquivalentTo(new[] { "one", "two" });

        await Assert.That((object)pipeline.Steps[0].Delegate).IsEqualTo(first);
        await Assert.That((object)pipeline.Steps[1].Delegate).IsEqualTo(second);
    }

    [Test]
    public async Task BuildQueryPipeline_UsesQueryDescriptor()
    {
        WorkflowDelegate handler = (config, context, token) => ValueTask.FromResult(new WorkflowResult("query"));
        var services = new ServiceCollection();
        services.AddKeyedSingleton<WorkflowDelegate>("query-step", (_, _) => handler);

        using var provider = services.BuildServiceProvider();
        var factory = new DefaultPipelineFactory(provider);

        var descriptor = new QueryPipelineDescriptor("query", new[] { "query-step" });
        var pipeline = factory.BuildQueryPipeline(descriptor);

        await Assert.That(pipeline.Name).IsEqualTo("query");
        await Assert.That(pipeline.Steps).HasSingleItem();
        await Assert.That((object)pipeline.Steps[0].Delegate).IsEqualTo(handler);
    }
}
