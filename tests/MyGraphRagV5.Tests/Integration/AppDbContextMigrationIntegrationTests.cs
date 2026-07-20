using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGraphRagV5.Data;
using Npgsql;

namespace MyGraphRagV5.Tests.Integration;

/// <summary>
/// Applies the real InitialCreate migration through the same runtime wiring as
/// <c>AddAppDatabase</c> (UseNpgsql + MigrationsHistoryTable("__EFMigrationsHistory", "app")),
/// against a real Postgres container - the migration apply step Task 2 deferred.
/// </summary>
[ClassDataSource<AgePgVectorFixture>(Shared = SharedType.PerAssembly)]
public sealed class AppDbContextMigrationIntegrationTests(AgePgVectorFixture fixture)
{
    [Before(Test)]
    public void SkipIfNoDocker()
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip.Test("Docker is not available");
        }
    }

    [Test]
    public async Task MigrateAsync_CreatesAppSchemaTables_AndIsIdempotentOnSecondRun()
    {
        var services = new ServiceCollection();
        services.AddAppDatabase(fixture.ConnectionString);
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        await Assert.That(await TableExistsAsync(fixture.ConnectionString, "app", "RagProjects")).IsTrue();
        await Assert.That(await TableExistsAsync(fixture.ConnectionString, "app", "IndexingRuns")).IsTrue();
        await Assert.That(await TableExistsAsync(fixture.ConnectionString, "app", "ChatSessions")).IsTrue();
        await Assert.That(await TableExistsAsync(fixture.ConnectionString, "app", "ChatMessages")).IsTrue();
        await Assert.That(await TableExistsAsync(fixture.ConnectionString, "app", "__EFMigrationsHistory")).IsTrue();

        // Re-applying must be a no-op (not 42P07 "relation already exists") - this is the Task 9
        // fix that pins the migrations-history table to the "app" schema so runtime MigrateAsync
        // finds the history row it already wrote instead of re-running InitialCreate from scratch.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await Assert.That(() => db.Database.MigrateAsync()).ThrowsNothing();
        }
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string schema, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = $1 AND table_name = $2);";
        command.Parameters.Add(new NpgsqlParameter { Value = schema });
        command.Parameters.Add(new NpgsqlParameter { Value = table });

        return (bool)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }
}
