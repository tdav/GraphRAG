using GraphRag.Logging;
using MyGraphRagV5.Indexing;

namespace MyGraphRagV5.Tests.Indexing;

public class RunProgressCallbacksTests
{
    [Test]
    public async Task WorkflowStart_ForwardsWorkflowNameWithCorrectRunId()
    {
        var runId = Guid.NewGuid();
        var sink = new FakeRunProgressSink();
        var callbacks = new RunProgressCallbacks(runId, sink);

        callbacks.WorkflowStart("create_base_text_units", instance: null);

        await Assert.That(sink.Calls).HasSingleItem();
        var call = sink.Calls.Single();
        await Assert.That(call.RunId).IsEqualTo(runId);
        await Assert.That(call.CurrentWorkflow).IsEqualTo("create_base_text_units");
        await Assert.That(call.Progress).IsNull();
    }

    [Test]
    public async Task ReportProgress_ForwardsProgressSnapshotWithCorrectRunId()
    {
        var runId = Guid.NewGuid();
        var sink = new FakeRunProgressSink();
        var callbacks = new RunProgressCallbacks(runId, sink);
        var snapshot = new ProgressSnapshot("Chunked document 1", 10, 3);

        callbacks.ReportProgress(snapshot);

        await Assert.That(sink.Calls).HasSingleItem();
        var call = sink.Calls.Single();
        await Assert.That(call.RunId).IsEqualTo(runId);
        await Assert.That(call.CurrentWorkflow).IsNull();
        await Assert.That(call.Progress).IsSameReferenceAs(snapshot);
    }

    [Test]
    public async Task WorkflowEnd_ForwardsWorkflowNameWithCorrectRunId()
    {
        var runId = Guid.NewGuid();
        var sink = new FakeRunProgressSink();
        var callbacks = new RunProgressCallbacks(runId, sink);

        callbacks.WorkflowEnd("create_base_text_units", instance: null);

        await Assert.That(sink.Calls).HasSingleItem();
        var call = sink.Calls.Single();
        await Assert.That(call.RunId).IsEqualTo(runId);
        await Assert.That(call.CurrentWorkflow).IsEqualTo("create_base_text_units");
        await Assert.That(call.Progress).IsNull();
    }

    [Test]
    public async Task PipelineStart_DoesNotThrow()
    {
        var sink = new FakeRunProgressSink();
        var callbacks = new RunProgressCallbacks(Guid.NewGuid(), sink);

        callbacks.PipelineStart(new[] { "create_base_text_units" });

        await Assert.That(sink.Calls).HasSingleItem();
    }

    [Test]
    public async Task PipelineEnd_DoesNotThrow()
    {
        var sink = new FakeRunProgressSink();
        var callbacks = new RunProgressCallbacks(Guid.NewGuid(), sink);

        callbacks.PipelineEnd(Array.Empty<GraphRag.Indexing.Runtime.PipelineRunResult>());

        await Assert.That(sink.Calls).HasSingleItem();
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
