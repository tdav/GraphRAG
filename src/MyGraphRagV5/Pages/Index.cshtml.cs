using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;

namespace MyGraphRagV5.Pages;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    private readonly AppDbContext db = db;

    public IReadOnlyList<RagProject> Projects { get; private set; } = [];

    public IReadOnlyList<RunRow> RecentRuns { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        this.Projects = await this.db.RagProjects
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var projectNames = this.Projects.ToDictionary(p => p.Id, p => p.Name);

        var runs = await this.db.IndexingRuns
            .OrderByDescending(r => r.StartedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        this.RecentRuns = runs
            .Select(r => new RunRow(r, projectNames.GetValueOrDefault(r.ProjectId, "—")))
            .ToList();
    }

    public sealed record RunRow(IndexingRun Run, string ProjectName);
}
