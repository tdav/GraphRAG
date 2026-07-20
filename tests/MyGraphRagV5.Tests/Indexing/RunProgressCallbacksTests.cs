using GraphRag.Logging;
using MyGraphRagV5.Indexing;

namespace MyGraphRagV5.Tests.Indexing;

public class RunProgressCallbacksTests
{
    [Fact]
    public void WorkflowStart_ForwardsWorkflowNameWithCorrectRunId()
    {
        var runId = Guid.NewGuid();
        var sink = new FakeRunProgressSink();
        var callbacks = new RunProgressCallbacks(runId, sink);

        callbacks.WorkflowStart("create_base_text_units", instance: null);

        var call = Assert.Single(sink.Calls);
        Assert.Equal(runId, call.RunId);
        Assert.Equal("create_base_text_units", call.CurrentWorkflow);
        Assert.Null(call.Progress);
    }

    [Fact]
    public void ReportProgress_ForwardsProgressSnapshotWithCorrectRunId()
    {
        var runId = Guid.NewGuid();
        var sink = new FakeRunProgressSink();
        var callbacks = new RunProgressCallbacks(runId, sink);
        var snapshot = new ProgressSnapshot("Chunked document 1", 10, 3);

        callbacks.ReportProgress(snapshot);

        var call = Assert.Single(sink.Calls);
        Assert.Equal(runId, call.RunId);
        Assert.Null(call.CurrentWorkflow);
        Assert.Same(snapshot, call.Progress);
    }

    [Fact]
    public void WorkflowEnd_ForwardsWorkflowNameWithCorrectRunId()
    {
        var runId = Guid.NewGuid();
        var sink = new FakeRunProgressSink();
        var callbacks = new RunProgressCallbacks(runId, sink);

        callbacks.WorkflowEnd("create_base_text_units", instance: null);

        var call = Assert.Single(sink.Calls);
        Assert.Equal(runId, call.RunId);
        Assert.Equal("create_base_text_units", call.CurrentWorkflow);
        Assert.Null(call.Progress);
    }

    [Fact]
    public void PipelineStart_DoesNotThrow()
    {
        var sink = new FakeRunProgressSink();
        var callbacks = new RunProgressCallbacks(Guid.NewGuid(), sink);

        callbacks.PipelineStart(new[] { "create_base_text_units" });

        Assert.Single(sink.Calls);
    }

    [Fact]
    public void PipelineEnd_DoesNotThrow()
    {
        var sink = new FakeRunProgressSink();
        var callbacks = new RunProgressCallbacks(Guid.NewGuid(), sink);

        callbacks.PipelineEnd(Array.Empty<GraphRag.Indexing.Runtime.PipelineRunResult>());

        Assert.Single(sink.Calls);
    }

    private sealed class FakeRunProgressSink : IRunProgressSink
    {
        public List<(Guid RunId, string? CurrentWorkflow, ProgressSnapshot? Progress)> Calls { get; } = new();

        public void Update(Guid runId, string? currentWorkflow, ProgressSnapshot? progress)
        {
            this.Calls.Add((runId, currentWorkflow, progress));
        }
    }
}
