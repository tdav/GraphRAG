using Microsoft.Extensions.DependencyInjection;

namespace MyGraphRagV5.Indexing;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the indexing orchestration singletons. <see cref="RunRegistry"/> is registered once
    /// and exposed both as itself and as <see cref="IRunProgressSink"/> (same instance) so the pipeline
    /// callbacks and the UI share one registry. Wired into Program.cs in Task 9.
    /// </summary>
    public static IServiceCollection AddIndexingServices(this IServiceCollection services)
    {
        services.AddSingleton<RunRegistry>();
        services.AddSingleton<IRunProgressSink>(sp => sp.GetRequiredService<RunRegistry>());
        services.AddSingleton<ProjectGraphStoreProvider>();
        services.AddSingleton<IndexingService>();
        return services;
    }
}
