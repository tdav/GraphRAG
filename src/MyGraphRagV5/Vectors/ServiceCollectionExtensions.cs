using GraphRag.Vectors;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MyGraphRagV5.Vectors;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPgVectorStore(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IVectorStore>(_ =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            return new PgVectorStore(dataSourceBuilder.Build());
        });

        return services;
    }
}
