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
[Collection(AgePgVectorCollection.Name)]
public sealed class AppDbContextMigrationIntegrationTests
{
    private readonly AgePgVectorFixture fixture;

    public AppDbContextMigrationIntegrationTests(AgePgVectorFixture fixture)
    {
        this.fixture = fixture;
    }

    [DockerAvailableFact]
    public async Task MigrateAsync_CreatesAppSchemaTables_AndIsIdempotentOnSecondRun()
    {
        var services = new ServiceCollection();
        services.AddAppDatabase(this.fixture.ConnectionString);
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        Assert.True(await TableExistsAsync(this.fixture.ConnectionString, "app", "RagProjects"));
        Assert.True(await TableExistsAsync(this.fixture.ConnectionString, "app", "IndexingRuns"));
        Assert.True(await TableExistsAsync(this.fixture.ConnectionString, "app", "ChatSessions"));
        Assert.True(await TableExistsAsync(this.fixture.ConnectionString, "app", "ChatMessages"));
        Assert.True(await TableExistsAsync(this.fixture.ConnectionString, "app", "__EFMigrationsHistory"));

        // Re-applying must be a no-op (not 42P07 "relation already exists") - this is the Task 9
        // fix that pins the migrations-history table to the "app" schema so runtime MigrateAsync
        // finds the history row it already wrote instead of re-running InitialCreate from scratch.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var exception = await Record.ExceptionAsync(() => db.Database.MigrateAsync());
            Assert.Null(exception);
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
