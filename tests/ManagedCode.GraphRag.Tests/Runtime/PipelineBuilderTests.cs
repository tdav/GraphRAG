using GraphRag.Indexing.Runtime;

namespace ManagedCode.GraphRag.Tests.Runtime;

public sealed class PipelineBuilderTests
{
    private static readonly WorkflowDelegate Noop = (config, context, token) => ValueTask.FromResult(new WorkflowResult(null));

    [Test]
    public async Task Build_CreatesPipelineWithNamedSteps()
    {
        var pipeline = new PipelineBuilder()
            .Named("demo")
            .Step("step1", Noop)
            .Step("step2", Noop)
            .Build();

        await Assert.That(pipeline.Name).IsEqualTo("demo");
        await Assert.That(pipeline.Names).IsEquivalentTo(new[] { "step1", "step2" });
    }

    [Test]
    public async Task Remove_EliminatesMatchingSteps()
    {
        var pipeline = new PipelineBuilder()
            .Step("alpha", Noop)
            .Step("beta", Noop)
            .Build();

        pipeline.Remove("alpha");

        await Assert.That(pipeline.Names).DoesNotContain("alpha");
        await Assert.That(pipeline.Names).Contains("beta");
    }
}
