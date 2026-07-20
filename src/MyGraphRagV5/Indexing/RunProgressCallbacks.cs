using GraphRag.Callbacks;
using GraphRag.Indexing.Runtime;
using GraphRag.Logging;

namespace MyGraphRagV5.Indexing;

/// <summary>
/// Forwards ManagedCode.GraphRag workflow callbacks to a run-scoped <see cref="IRunProgressSink"/>
/// so the web UI can observe indexing progress for a specific run.
/// </summary>
public sealed class RunProgressCallbacks(Guid runId, IRunProgressSink sink) : IWorkflowCallbacks
{
    private readonly Guid runId = runId;
    private readonly IRunProgressSink sink = sink ?? throw new ArgumentNullException(nameof(sink));

    public void PipelineStart(IReadOnlyList<string> names)
    {
        this.sink.Update(this.runId, null, null);
    }

    public void PipelineEnd(IReadOnlyList<PipelineRunResult> results)
    {
        this.sink.Update(this.runId, null, null);
    }

    public void WorkflowStart(string name, object? instance)
    {
        this.sink.Update(this.runId, name, null);
    }

    public void WorkflowEnd(string name, object? instance)
    {
        this.sink.Update(this.runId, name, null);
    }

    public void ReportProgress(ProgressSnapshot progress)
    {
        this.sink.Update(this.runId, null, progress);
    }
}
