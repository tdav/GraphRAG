using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;
using MyGraphRagV5.Pages.Projects;

namespace MyGraphRagV5.Tests.Pages.Projects;

public class ProjectNamingTests
{
    [Fact]
    public async Task DeriveUniqueAsync_SanitizesNameIntoSafeIdentifiers()
    {
        using var db = CreateDb();

        var (graphName, vectorCollection) = await ProjectNaming.DeriveUniqueAsync(db, "My Cool Project!", CancellationToken.None);

        Assert.Equal("g_my_cool_project_", graphName);
        Assert.Equal("my_cool_project_", vectorCollection);
    }

    [Fact]
    public async Task DeriveUniqueAsync_FallsBackToPlaceholder_WhenNameSanitizesToNothing()
    {
        using var db = CreateDb();

        var (graphName, vectorCollection) = await ProjectNaming.DeriveUniqueAsync(db, "???", CancellationToken.None);

        Assert.Equal("g_project", graphName);
        Assert.Equal("project", vectorCollection);
    }

    [Fact]
    public async Task DeriveUniqueAsync_AppendsSuffixOnCollision()
    {
        using var db = CreateDb();
        db.RagProjects.Add(new RagProject
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            SourceFolder = "C:/src",
            GraphName = "g_acme",
            VectorCollection = "acme",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var (graphName, vectorCollection) = await ProjectNaming.DeriveUniqueAsync(db, "Acme", CancellationToken.None);

        Assert.Equal("g_acme_1", graphName);
        Assert.Equal("acme_1", vectorCollection);
    }

    [Fact]
    public async Task DeriveUniqueAsync_KeepsIncrementingUntilFree()
    {
        using var db = CreateDb();
        db.RagProjects.AddRange(
            new RagProject { Id = Guid.NewGuid(), Name = "a", SourceFolder = "s", GraphName = "g_acme", VectorCollection = "acme", CreatedAt = DateTimeOffset.UtcNow },
            new RagProject { Id = Guid.NewGuid(), Name = "b", SourceFolder = "s", GraphName = "g_acme_1", VectorCollection = "acme_1", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var (graphName, vectorCollection) = await ProjectNaming.DeriveUniqueAsync(db, "Acme", CancellationToken.None);

        Assert.Equal("g_acme_2", graphName);
        Assert.Equal("acme_2", vectorCollection);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
