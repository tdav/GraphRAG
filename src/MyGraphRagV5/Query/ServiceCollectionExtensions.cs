using Microsoft.Extensions.DependencyInjection;
using MyGraphRagV5.Indexing;

namespace MyGraphRagV5.Query;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RagQueryService"/> plus the <see cref="IProjectGraphStoreProvider"/>
    /// mapping onto the existing <see cref="ProjectGraphStoreProvider"/> singleton. Requires
    /// <c>AddIndexingServices</c> (registers <see cref="ProjectGraphStoreProvider"/>) to already be
    /// called. Wired into Program.cs in Task 9.
    /// </summary>
    public static IServiceCollection AddRagQuery(this IServiceCollection services)
    {
        services.AddSingleton<IProjectGraphStoreProvider>(sp => sp.GetRequiredService<ProjectGraphStoreProvider>());
        services.AddSingleton<RagQueryService>();
        return services;
    }
}
