using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;
using MyGraphRagV5.Pages.Projects;

namespace MyGraphRagV5.Tests.Pages.Projects;

public class ProjectNamingTests
{
    [Test]
    public async Task DeriveUniqueAsync_SanitizesNameIntoSafeIdentifiers()
    {
        using var db = CreateDb();

        var (graphName, vectorCollection) = await ProjectNaming.DeriveUniqueAsync(db, "My Cool Project!", CancellationToken.None);

        await Assert.That(graphName).IsEqualTo("g_my_cool_project_");
        await Assert.That(vectorCollection).IsEqualTo("my_cool_project_");
    }

    [Test]
    public async Task DeriveUniqueAsync_FallsBackToPlaceholder_WhenNameSanitizesToNothing()
    {
        using var db = CreateDb();

        var (graphName, vectorCollection) = await ProjectNaming.DeriveUniqueAsync(db, "???", CancellationToken.None);

        await Assert.That(graphName).IsEqualTo("g_project");
        await Assert.That(vectorCollection).IsEqualTo("project");
    }

    [Test]
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

        await Assert.That(graphName).IsEqualTo("g_acme_1");
        await Assert.That(vectorCollection).IsEqualTo("acme_1");
    }

    [Test]
    public async Task DeriveUniqueAsync_KeepsIncrementingUntilFree()
    {
        using var db = CreateDb();
        db.RagProjects.AddRange(
            new RagProject { Id = Guid.NewGuid(), Name = "a", SourceFolder = "s", GraphName = "g_acme", VectorCollection = "acme", CreatedAt = DateTimeOffset.UtcNow },
            new RagProject { Id = Guid.NewGuid(), Name = "b", SourceFolder = "s", GraphName = "g_acme_1", VectorCollection = "acme_1", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var (graphName, vectorCollection) = await ProjectNaming.DeriveUniqueAsync(db, "Acme", CancellationToken.None);

        await Assert.That(graphName).IsEqualTo("g_acme_2");
        await Assert.That(vectorCollection).IsEqualTo("acme_2");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
