using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;
using MyGraphRagV5.Indexing;

namespace MyGraphRagV5.Pages.Projects;

/// <summary>
/// Rendering model for the <c>_RunProgress</c> partial. Status/timestamps/error come from the
/// authoritative <see cref="IndexingRun"/> row; live workflow/item counters come from
/// <see cref="RunRegistry"/> (empty once the run has finished, since <c>IndexingService</c> removes
/// its entry in the same step that persists the terminal status).
/// </summary>
public sealed record RunProgressViewModel(
    Guid RunId,
    Guid ProjectId,
    IndexingRunStatus Status,
    string? CurrentWorkflow,
    int? CompletedItems,
    int? TotalItems,
    string? Error,
    DateTimeOffset? CompletedAt)
{
    public bool IsTerminal => this.Status
        is IndexingRunStatus.Succeeded or IndexingRunStatus.Failed or IndexingRunStatus.Cancelled;

    public static async Task<RunProgressViewModel?> BuildAsync(
        AppDbContext db, RunRegistry runRegistry, Guid runId, CancellationToken cancellationToken)
    {
        var run = await db.IndexingRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return null;
        }

        var live = runRegistry.GetProgress(runId);
        return new RunProgressViewModel(
            run.Id,
            run.ProjectId,
            run.Status,
            live?.CurrentWorkflow,
            live?.Progress?.CompletedItems,
            live?.Progress?.TotalItems,
            run.Error,
            run.CompletedAt);
    }
}
