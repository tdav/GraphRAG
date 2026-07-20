using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;
using MyGraphRagV5.Indexing;
using MyGraphRagV5.Pages.Projects;

namespace MyGraphRagV5.Tests.Pages.Projects;

public class DetailsModelTests
{
    [Fact]
    public async Task OnPostStartAsync_RegistryHasActiveRunButRowNotCommittedYet_RedirectsInsteadOfThrowing()
    {
        // Simulates the concurrent-start race: IndexingService.StartRunAsync adds the run to
        // RunRegistry (HasActiveRun -> true) before its IndexingRun row's SaveChangesAsync commits.
        // A second Start landing in that gap must not surface LatestRunningRunIdAsync's now-defunct
        // FirstAsync "Sequence contains no elements" as an unhandled 500.
        using var db = CreateDb();
        var projectId = Guid.NewGuid();
        db.RagProjects.Add(new RagProject
        {
            Id = projectId,
            Name = "Acme",
            SourceFolder = "C:/src",
            GraphName = "g_acme",
            VectorCollection = "acme",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var registry = new RunRegistry();
        using var cts = new CancellationTokenSource();
        registry.TryRegister(Guid.NewGuid(), projectId, cts); // registered, but no IndexingRun row exists yet

        // Never reached: HasActiveRun(projectId) is already true, so OnPostStartAsync takes the
        // LatestRunningRunIdAsync branch and never calls into IndexingService.
        var indexingService = new IndexingService(null!, registry, null!, null!, null!, null!, null!, null!);
        var model = new DetailsModel(db, registry, indexingService)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await model.OnPostStartAsync(projectId, CancellationToken.None);

        Assert.IsType<ContentResult>(result);
        Assert.Equal($"/Projects/Details/{projectId}", model.Response.Headers["HX-Redirect"].ToString());
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
