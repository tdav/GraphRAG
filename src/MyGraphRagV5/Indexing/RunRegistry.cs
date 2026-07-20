using System.Collections.Concurrent;
using GraphRag.Logging;

namespace MyGraphRagV5.Indexing;

/// <summary>
/// In-process registry of live indexing runs. Receives pipeline progress through
/// <see cref="IRunProgressSink"/> and stores the latest workflow / progress per run so the web UI
/// can poll it. Enforces one active run per project (a second concurrent start is rejected).
/// </summary>
public sealed class RunRegistry : IRunProgressSink
{
    private readonly ConcurrentDictionary<Guid, RunHandle> runs = new();
    private readonly ConcurrentDictionary<Guid, Guid> activeProjects = new();

    /// <summary>
    /// Atomically reserves the project slot and registers the run. Returns <c>null</c> when the
    /// project already has an active run, which the caller must treat as a rejected start.
    /// </summary>
    public RunHandle? TryRegister(Guid runId, Guid projectId, CancellationTokenSource cts)
    {
        ArgumentNullException.ThrowIfNull(cts);

        if (!this.activeProjects.TryAdd(projectId, runId))
        {
            return null;
        }

        var handle = new RunHandle(runId, projectId, cts);
        this.runs[runId] = handle;
        return handle;
    }

    public bool HasActiveRun(Guid projectId) => this.activeProjects.ContainsKey(projectId);

    public RunHandle? Get(Guid runId) => this.runs.TryGetValue(runId, out var handle) ? handle : null;

    /// <summary>Live progress for a run, or <c>null</c> if it is not (or no longer) active.</summary>
    public RunProgress? GetProgress(Guid runId)
        => this.runs.TryGetValue(runId, out var handle) ? handle.SnapshotProgress() : null;

    /// <summary>
    /// Requests cancellation of a run. Returns <c>false</c> if the run is unknown, or if it just
    /// finished and disposed its <see cref="CancellationTokenSource"/> concurrently with this call.
    /// </summary>
    public bool CancelRun(Guid runId)
    {
        if (!this.runs.TryGetValue(runId, out var handle))
        {
            return false;
        }

        try
        {
            handle.Cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The background task's finally block (Remove + Cts.Dispose) can race ahead of us between
            // TryGetValue above and Cancel here. The run already finished, so there is nothing left to
            // cancel; treat it as a no-op rather than letting the exception surface to the HTTP caller.
            return false;
        }

        return true;
    }

    /// <summary>Removes a finished run and frees its project slot.</summary>
    public void Remove(Guid runId)
    {
        if (this.runs.TryRemove(runId, out var handle))
        {
            this.activeProjects.TryRemove(handle.ProjectId, out _);
        }
    }

    void IRunProgressSink.Update(Guid runId, string? currentWorkflow, ProgressSnapshot? progress)
    {
        if (this.runs.TryGetValue(runId, out var handle))
        {
            handle.Update(currentWorkflow, progress);
        }
    }
}

/// <summary>Mutable per-run state: cancellation source, background task, and latest live progress.</summary>
public sealed class RunHandle
{
    private readonly object gate = new();
    private string? currentWorkflow;
    private ProgressSnapshot? progress;

    internal RunHandle(Guid runId, Guid projectId, CancellationTokenSource cts)
    {
        this.RunId = runId;
        this.ProjectId = projectId;
        this.Cts = cts;
    }

    public Guid RunId { get; }

    public Guid ProjectId { get; }

    internal CancellationTokenSource Cts { get; }

    /// <summary>The tracked background task; set once the run is launched.</summary>
    public Task? Task { get; private set; }

    internal void AttachTask(Task task) => this.Task = task;

    // Workflow callbacks fire either a workflow name (WorkflowStart/End) or a progress snapshot
    // (ReportProgress), never both at once. Only overwrite the field that was actually provided so a
    // progress tick does not wipe the current workflow name and vice-versa.
    internal void Update(string? workflow, ProgressSnapshot? snapshot)
    {
        lock (this.gate)
        {
            if (workflow is not null)
            {
                this.currentWorkflow = workflow;
            }

            if (snapshot is not null)
            {
                this.progress = snapshot;
            }
        }
    }

    internal RunProgress SnapshotProgress()
    {
        lock (this.gate)
        {
            return new RunProgress(this.RunId, this.ProjectId, this.currentWorkflow, this.progress);
        }
    }
}

/// <summary>Immutable snapshot of a run's live progress for the UI.</summary>
public sealed record RunProgress(Guid RunId, Guid ProjectId, string? CurrentWorkflow, ProgressSnapshot? Progress)
{
    /// <summary>Completion percentage derived from the latest progress snapshot, if available.</summary>
    public double? Percent =>
        this.Progress is { TotalItems: > 0 } p && p.CompletedItems is { } done
            ? Math.Clamp(100.0 * done / p.TotalItems.Value, 0, 100)
            : null;
}
