using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using Npgsql;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace MyGraphRagV5.Tests.Integration;

/// <summary>
/// Builds the age-pgvector.Dockerfile image once (Testcontainers reuses the same
/// <see cref="IFutureDockerImage"/> for every container created from it) and starts one
/// Postgres container from it for the whole "AgePgVector" test collection. Reuses the
/// AGE-enablement trick from tests/ManagedCode.GraphRag.Tests/GraphRagApplicationFixture.cs
/// (ALTER SYSTEM SET shared_preload_libraries + pg_reload_conf).
/// </summary>
public sealed class AgePgVectorFixture : IAsyncInitializer, IAsyncDisposable
{
    private const string Database = "mygraphragv5_test";
    private const string Username = "postgres";
    private const string Password = "postgres";

    private IFutureDockerImage? image;
    private PostgreSqlContainer? container;

    public string ConnectionString => this.container?.GetConnectionString()
        ?? throw new InvalidOperationException("AgePgVectorFixture has not been initialized.");

    public async Task InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable)
        {
            // No test in this collection will actually run (they're all [DockerAvailableFact]
            // skips), but guard anyway in case something else constructs this fixture directly.
            return;
        }

        this.image = new ImageFromDockerfileBuilder()
            .WithName("mygraphragv5-tests-age-pgvector:latest")
            .WithDockerfileDirectory(CommonDirectoryPath.GetCallerFileDirectory(), "..")
            .WithDockerfile("age-pgvector.Dockerfile")
            .WithCleanUp(false) // keep the image around so Docker's layer cache speeds up later runs
            .Build();

        await this.image.CreateAsync().ConfigureAwait(false);

        this.container = new PostgreSqlBuilder()
            .WithImage(this.image)
            .WithDatabase(Database)
            .WithUsername(Username)
            .WithPassword(Password)
            .WithCleanUp(true)
            .Build();

        await this.container.StartAsync().ConfigureAwait(false);
        await EnableAgeAsync(this.container.GetConnectionString()).ConfigureAwait(false);

        // The Global Constraints assume "vector" is already installed on the target database
        // (it is, on the real :6000 instance). PgVectorStore itself never runs CREATE EXTENSION,
        // so the fixture has to set up that same precondition for a fresh test container.
        await EnsureVectorExtensionAsync(this.container.GetConnectionString()).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (this.container is not null)
        {
            await this.container.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task EnableAgeAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using (var setLibraries = connection.CreateCommand())
        {
            setLibraries.CommandText = "ALTER SYSTEM SET shared_preload_libraries = 'age';";
            await setLibraries.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using var reload = connection.CreateCommand();
        reload.CommandText = "SELECT pg_reload_conf();";
        await reload.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task EnsureVectorExtensionAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using var createExtension = connection.CreateCommand();
        createExtension.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
        await createExtension.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
