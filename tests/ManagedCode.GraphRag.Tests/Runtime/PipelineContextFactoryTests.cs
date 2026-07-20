using GraphRag.Callbacks;
using GraphRag.Indexing.Runtime;
using GraphRag.Storage;
using ManagedCode.GraphRag.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.GraphRag.Tests.Runtime;

public sealed class PipelineContextFactoryTests
{
    [Test]
    public async Task Create_UsesProvidedComponents()
    {
        var input = new MemoryPipelineStorage();
        var output = new MemoryPipelineStorage();
        var previous = new MemoryPipelineStorage();
        var cache = new StubPipelineCache();
        var callbacks = WorkflowCallbacksManagerFactory();
        var stats = new PipelineRunStats();
        var state = new PipelineState();
        var services = new ServiceCollection().BuildServiceProvider();
        var context = PipelineContextFactory.Create(input, output, previous, cache, callbacks, stats, state, services, new Dictionary<string, object?> { ["flag"] = true });

        await Assert.That(context.InputStorage).IsSameReferenceAs(input);
        await Assert.That(context.Cache).IsSameReferenceAs(cache);
        await Assert.That((bool)context.Items["flag"]!).IsTrue();
    }

    [Test]
    public async Task Create_ProvidesDefaultsWhenNull()
    {
        var context = PipelineContextFactory.Create();

        await Assert.That(context.InputStorage).IsTypeOf<MemoryPipelineStorage>();
        await Assert.That(context.Cache).IsNull();
        await Assert.That(context.Services).IsNotNull();
    }

    private static IWorkflowCallbacks WorkflowCallbacksManagerFactory() => new WorkflowCallbacksManager();
}
