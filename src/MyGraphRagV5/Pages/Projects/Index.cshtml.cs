using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;

namespace MyGraphRagV5.Pages.Projects;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    private readonly AppDbContext db = db;

    public IReadOnlyList<ProjectRow> Rows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var projects = await this.db.RagProjects
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var projectIds = projects.Select(p => p.Id).ToList();
        var runs = await this.db.IndexingRuns
            .AsNoTracking()
            .Where(r => projectIds.Contains(r.ProjectId))
            .ToListAsync(cancellationToken);

        var latestByProject = runs
            .GroupBy(r => r.ProjectId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.StartedAt).First());

        this.Rows = projects
            .Select(p => new ProjectRow(p, latestByProject.GetValueOrDefault(p.Id)))
            .ToList();
    }

    public sealed record ProjectRow(RagProject Project, IndexingRun? LatestRun);
}
