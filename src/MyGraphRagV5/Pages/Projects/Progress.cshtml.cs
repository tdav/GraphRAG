using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyGraphRagV5.Data;
using MyGraphRagV5.Indexing;

namespace MyGraphRagV5.Pages.Projects;

/// <summary>
/// Renders the <c>_RunProgress</c> fragment for one run. Polled by that fragment itself
/// (<c>hx-get="/Projects/Progress?runId=..." hx-trigger="every 2s"</c>) while the run is active;
/// the fragment stops including the polling attributes once <see cref="RunProgressViewModel.IsTerminal"/>
/// is true, which is what actually stops the polling loop.
/// </summary>
public sealed class ProgressModel(AppDbContext db, RunRegistry runRegistry, IndexingService indexingService) : PageModel
{
    private readonly AppDbContext db = db;
    private readonly RunRegistry runRegistry = runRegistry;
    private readonly IndexingService indexingService = indexingService;

    /// <summary>Null when the run (or its project, via cascade delete) no longer exists.</summary>
    public RunProgressViewModel? ViewModel { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid runId, CancellationToken cancellationToken)
    {
        // Returning NotFound here would leave the polling _RunProgress fragment's hx-trigger attached
        // forever -- htmx only swaps on 2xx, so a 404 means the poll just keeps firing every 2s. Render
        // a 200 fragment with no hx-* attributes instead; that is what actually stops the polling loop.
        this.ViewModel = await RunProgressViewModel.BuildAsync(this.db, this.runRegistry, runId, cancellationToken);
        return this.Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid runId, CancellationToken cancellationToken)
    {
        this.indexingService.CancelRun(runId);
        return await this.OnGetAsync(runId, cancellationToken);
    }
}
