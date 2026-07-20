using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyGraphRagV5.Data;

/// <summary>
/// Lets `dotnet ef migrations add` build <see cref="AppDbContext"/> at design time without a running database.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    // ponytail: local dev connection string, not a secret; real wiring (appsettings/user-secrets) lands in Task 9.
    private const string DevConnectionString = "Host=localhost;Port=6000;Database=graphdb;Username=postgres;Password=M@y_Passw0rd";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MYGRAPHRAGV5_CONNECTION_STRING") ?? DevConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "app"));

        return new AppDbContext(optionsBuilder.Options);
    }
}
