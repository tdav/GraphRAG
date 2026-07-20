using GraphRag.Callbacks;
using GraphRag.Indexing.Runtime;
using GraphRag.Logging;

namespace ManagedCode.GraphRag.Tests.Callbacks;

public sealed class WorkflowCallbacksManagerTests
{
    [Test]
    public async Task Manager_ForwardsEventsToRegisteredCallbacks()
    {
        var manager = new WorkflowCallbacksManager();
        var spy = new SpyCallbacks();
        manager.Register(spy);

        var pipelineNames = new[] { "one", "two" };
        manager.PipelineStart(pipelineNames);
        manager.WorkflowStart("step", null);
        manager.ReportProgress(new ProgressSnapshot("stage", 1, 2));
        manager.WorkflowEnd("step", null);
        manager.PipelineEnd(Array.Empty<PipelineRunResult>());

        await Assert.That(spy.PipelineStarted).IsTrue();
        await Assert.That(spy.WorkflowStarted).IsTrue();
        await Assert.That(spy.WorkflowEnded).IsTrue();
        await Assert.That(spy.ProgressReported).IsTrue();
        await Assert.That(spy.PipelineEnded).IsTrue();
    }

    private sealed class SpyCallbacks : IWorkflowCallbacks
    {
        public bool PipelineStarted { get; private set; }
        public bool PipelineEnded { get; private set; }
        public bool WorkflowStarted { get; private set; }
        public bool WorkflowEnded { get; private set; }
        public bool ProgressReported { get; private set; }

        public void PipelineStart(IReadOnlyList<string> names) => PipelineStarted = true;

        public void PipelineEnd(IReadOnlyList<PipelineRunResult> results) => PipelineEnded = true;

        public void WorkflowStart(string name, object? instance) => WorkflowStarted = true;

        public void WorkflowEnd(string name, object? instance) => WorkflowEnded = true;

        public void ReportProgress(ProgressSnapshot progress) => ProgressReported = true;
    }
}
