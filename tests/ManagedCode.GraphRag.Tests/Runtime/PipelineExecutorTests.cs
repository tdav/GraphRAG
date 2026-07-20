using GraphRag.Callbacks;
using GraphRag.Config;
using GraphRag.Indexing.Runtime;
using GraphRag.Logging;
using GraphRag.Storage;
using ManagedCode.GraphRag.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManagedCode.GraphRag.Tests.Runtime;

public sealed class PipelineExecutorTests
{
    [Test]
    public async Task ExecuteAsync_StopsOnException()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var context = new PipelineRunContext(
            new MemoryPipelineStorage(),
            new MemoryPipelineStorage(),
            new MemoryPipelineStorage(),
            new StubPipelineCache(),
            NoopWorkflowCallbacks.Instance,
            new PipelineRunStats(),
            new PipelineState(),
            services);

        var pipeline = new WorkflowPipeline("test", new[]
        {
            new WorkflowStep("ok", (cfg, ctx, token) => ValueTask.FromResult(new WorkflowResult("done"))),
            new WorkflowStep("boom", (cfg, ctx, token) => throw new InvalidOperationException("fail")),
            new WorkflowStep("skipped", (cfg, ctx, token) => throw new InvalidOperationException("should not run"))
        });

        var executor = new PipelineExecutor(new NullLogger<PipelineExecutor>());
        var results = new List<PipelineRunResult>();

        await foreach (var result in executor.ExecuteAsync(pipeline, new GraphRagConfig(), context))
        {
            results.Add(result);
        }

        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Errors).IsNull();
        await Assert.That(results[1].Errors).IsNotNull();
        await Assert.That(results[1].Workflow).IsEqualTo("boom");
    }

    [Test]
    public async Task ExecuteAsync_HonoursStopSignal()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var context = new PipelineRunContext(
            new MemoryPipelineStorage(),
            new MemoryPipelineStorage(),
            new MemoryPipelineStorage(),
            new StubPipelineCache(),
            NoopWorkflowCallbacks.Instance,
            new PipelineRunStats(),
            new PipelineState(),
            services);

        var pipeline = new WorkflowPipeline("stop", new[]
        {
            new WorkflowStep("first", (cfg, ctx, token) => ValueTask.FromResult(new WorkflowResult(null, true))),
            new WorkflowStep("second", (cfg, ctx, token) => ValueTask.FromResult(new WorkflowResult("should not happen")))
        });

        var executor = new PipelineExecutor(new NullLogger<PipelineExecutor>());
        var outputs = new List<PipelineRunResult>();
        await foreach (var result in executor.ExecuteAsync(pipeline, new GraphRagConfig(), context))
        {
            outputs.Add(result);
        }

        await Assert.That(outputs).HasSingleItem();
        await Assert.That(outputs[0].Workflow).IsEqualTo("first");
    }

    [Test]
    public async Task ExecuteAsync_InvokesCallbacksAndUpdatesStats()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var callbacks = new RecordingCallbacks();
        var stats = new PipelineRunStats();
        var context = new PipelineRunContext(
            new MemoryPipelineStorage(),
            new MemoryPipelineStorage(),
            new MemoryPipelineStorage(),
            new StubPipelineCache(),
            callbacks,
            stats,
            new PipelineState(),
            services);

        var pipeline = new WorkflowPipeline("stats", new[]
        {
            new WorkflowStep("first", async (cfg, ctx, token) =>
            {
                await Task.Delay(5, token);
                return new WorkflowResult("ok");
            }),
            new WorkflowStep("second", (cfg, ctx, token) => ValueTask.FromResult(new WorkflowResult("done")))
        });

        var executor = new PipelineExecutor(new NullLogger<PipelineExecutor>());
        var results = new List<PipelineRunResult>();

        await foreach (var result in executor.ExecuteAsync(pipeline, new GraphRagConfig(), context))
        {
            results.Add(result);
        }

        await Assert.That(callbacks.WorkflowStarts).IsEquivalentTo(new[] { "first", "second" });
        await Assert.That(callbacks.WorkflowEnds).IsEquivalentTo(callbacks.WorkflowStarts);
        await Assert.That(callbacks.PipelineEndResults?.Count).IsEqualTo(2);
        await Assert.That(callbacks.PipelineStartedWith?.SequenceEqual(pipeline.Names) ?? false).IsTrue();

        await Assert.That(results.Count).IsEqualTo(2);
        foreach (var r in results)
        {
            await Assert.That(r.Errors).IsNull();
        }

        await Assert.That(stats.TotalRuntime >= 0).IsTrue();
        await Assert.That(stats.Workflows.ContainsKey("first")).IsTrue();
        await Assert.That(stats.Workflows["first"].ContainsKey("overall")).IsTrue();
        await Assert.That(stats.Workflows.ContainsKey("second")).IsTrue();
        await Assert.That(stats.Workflows["second"].ContainsKey("overall")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_RecordsExceptionInResultsAndStats()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var stats = new PipelineRunStats();
        var callbacks = new RecordingCallbacks();
        var context = new PipelineRunContext(
            new MemoryPipelineStorage(),
            new MemoryPipelineStorage(),
            new MemoryPipelineStorage(),
            new StubPipelineCache(),
            callbacks,
            stats,
            new PipelineState(),
            services);

        var failure = new InvalidOperationException("fail");
        var pipeline = new WorkflowPipeline("failing", new[]
        {
            new WorkflowStep("good", (cfg, ctx, token) => ValueTask.FromResult(new WorkflowResult("done"))),
            new WorkflowStep("bad", (cfg, ctx, token) => throw failure),
            new WorkflowStep("skipped", (cfg, ctx, token) => ValueTask.FromResult(new WorkflowResult("nope")))
        });

        var executor = new PipelineExecutor(new NullLogger<PipelineExecutor>());
        var results = new List<PipelineRunResult>();

        await foreach (var result in executor.ExecuteAsync(pipeline, new GraphRagConfig(), context))
        {
            results.Add(result);
        }

        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Errors).IsNull();
        var errorResult = results[1];
        await Assert.That(errorResult.Errors).IsNotNull();
        await Assert.That(errorResult.Errors!).HasSingleItem();
        var captured = errorResult.Errors!.Single();
        await Assert.That(captured).IsSameReferenceAs(failure);

        await Assert.That(callbacks.WorkflowStarts).IsEquivalentTo(new[] { "good", "bad" });
        await Assert.That(callbacks.WorkflowEnds).IsEquivalentTo(callbacks.WorkflowStarts);
        await Assert.That(callbacks.PipelineEndResults?.Count).IsEqualTo(2);

        await Assert.That(stats.Workflows.ContainsKey("good")).IsTrue();
        await Assert.That(stats.Workflows.ContainsKey("bad")).IsTrue();
        await Assert.That(stats.Workflows.ContainsKey("skipped")).IsFalse();
        await Assert.That(stats.TotalRuntime >= 0).IsTrue();
    }

    private sealed class RecordingCallbacks : IWorkflowCallbacks
    {
        public IReadOnlyList<string>? PipelineStartedWith { get; private set; }
        public List<string> WorkflowStarts { get; } = new();
        public List<string> WorkflowEnds { get; } = new();
        public IReadOnlyList<PipelineRunResult>? PipelineEndResults { get; private set; }
        public List<ProgressSnapshot> ProgressUpdates { get; } = new();

        public void PipelineStart(IReadOnlyList<string> names)
        {
            PipelineStartedWith = names.ToArray();
        }

        public void PipelineEnd(IReadOnlyList<PipelineRunResult> results)
        {
            PipelineEndResults = results.ToArray();
        }

        public void WorkflowStart(string name, object? instance)
        {
            WorkflowStarts.Add(name);
        }

        public void WorkflowEnd(string name, object? instance)
        {
            WorkflowEnds.Add(name);
        }

        public void ReportProgress(ProgressSnapshot progress)
        {
            ProgressUpdates.Add(progress);
        }
    }
}
