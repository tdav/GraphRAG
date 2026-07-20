using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;
using MyGraphRagV5.Indexing;

namespace MyGraphRagV5.Pages.Projects;

public sealed class DetailsModel(AppDbContext db, RunRegistry runRegistry, IndexingService indexingService) : PageModel
{
    private readonly AppDbContext db = db;
    private readonly RunRegistry runRegistry = runRegistry;
    private readonly IndexingService indexingService = indexingService;

    public RagProject Project { get; private set; } = null!;

    public IReadOnlyList<IndexingRun> Runs { get; private set; } = [];

    /// <summary>Pre-rendered progress for an already-active run, so polling starts immediately on page load.</summary>
    public RunProgressViewModel? ActiveRun { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await this.db.RagProjects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return this.NotFound();
        }

        this.Project = project;

        this.Runs = await this.db.IndexingRuns.AsNoTracking()
            .Where(r => r.ProjectId == id)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(cancellationToken);

        var active = this.Runs.FirstOrDefault(r => r.Status is IndexingRunStatus.Running or IndexingRunStatus.Pending);
        if (active is not null)
        {
            this.ActiveRun = await RunProgressViewModel.BuildAsync(this.db, this.runRegistry, active.Id, cancellationToken);
        }

        return this.Page();
    }

    /// <summary>
    /// Starts a new run, or -- if one is already active for this project -- falls back to showing that
    /// run's progress instead of surfacing IndexingService's "already has an active run" exception.
    /// </summary>
    public async Task<IActionResult> OnPostStartAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await this.db.RagProjects.AnyAsync(p => p.Id == id, cancellationToken))
        {
            return this.NotFound();
        }

        Guid? runId;
        if (this.runRegistry.HasActiveRun(id))
        {
            runId = await this.LatestRunningRunIdAsync(id, cancellationToken);
        }
        else
        {
            try
            {
                runId = await this.indexingService.StartRunAsync(id, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Lost a race against another request starting a run for this project concurrently.
                runId = await this.LatestRunningRunIdAsync(id, cancellationToken);
            }
        }

        if (runId is null)
        {
            // RunRegistry registers a run before its IndexingRun row commits (see StartRunAsync), so
            // a second Start can land in that gap: HasActiveRun is already true, but no Running row
            // exists yet for LatestRunningRunIdAsync to find. Rather than surface that as a 500, tell
            // htmx to do a full reload of Details -- by the time the browser re-requests it the row
            // has virtually always committed, and Details' own OnGetAsync tolerates "no row yet" fine.
            this.Response.Headers["HX-Redirect"] = $"/Projects/Details/{id}";
            return this.Content(string.Empty);
        }

        return this.RedirectToPage("Progress", new { runId });
    }

    private async Task<Guid?> LatestRunningRunIdAsync(Guid projectId, CancellationToken cancellationToken) =>
        await this.db.IndexingRuns.AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.Status == IndexingRunStatus.Running)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        // ponytail: app rows only. The AGE graph and pgvector table for this project are left in
        // place -- dropping them is out of scope for 8b (noted in the task report).
        await this.db.RagProjects.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
        return this.RedirectToPage("Index");
    }
}
