using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MyGraphRagV5.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppDatabase(this IServiceCollection services, string connectionString)
    {
        // The migrations history table must live in the "app" schema to match the design-time
        // factory (AppDbContextFactory); otherwise runtime MigrateAsync reads an empty history in
        // "public", tries to re-apply InitialCreate, and fails with 42P07 (tables already exist).
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "app")));
        return services;
    }
}
