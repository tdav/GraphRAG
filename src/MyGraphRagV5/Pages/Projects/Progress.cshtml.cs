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

    public RunProgressViewModel ViewModel { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid runId, CancellationToken cancellationToken)
    {
        var vm = await RunProgressViewModel.BuildAsync(this.db, this.runRegistry, runId, cancellationToken);
        if (vm is null)
        {
            return this.NotFound();
        }

        this.ViewModel = vm;
        return this.Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid runId, CancellationToken cancellationToken)
    {
        this.indexingService.CancelRun(runId);
        return await this.OnGetAsync(runId, cancellationToken);
    }
}
