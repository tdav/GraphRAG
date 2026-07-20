using System.Collections.Concurrent;
using GraphRag.Graphs;
using GraphRag.Storage.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyGraphRagV5.Indexing;

/// <summary>
/// Builds and caches one <see cref="PostgresGraphStore"/> per project graph name at runtime.
/// Projects are created after startup, so the start-time keyed <c>AddPostgresGraphStore</c>
/// registration cannot be used; this provider mirrors that wiring on demand and initializes
/// (creates the AGE graph) exactly once per graph name.
/// </summary>
public sealed class ProjectGraphStoreProvider(IConfiguration configuration, ILoggerFactory loggerFactory)
    : IAsyncDisposable
{
    private readonly string connectionString = configuration.GetConnectionString("GraphDb")
        ?? throw new InvalidOperationException("Connection string 'GraphDb' is not configured.");
    private readonly ILoggerFactory loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    private readonly ConcurrentDictionary<string, Lazy<Task<PostgresGraphStore>>> stores =
        new(StringComparer.Ordinal);

    /// <summary>Returns an initialized graph store for the given graph name, building it on first use.</summary>
    public async Task<IGraphStore> GetStoreAsync(string graphName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);

        var lazy = this.stores.GetOrAdd(
            graphName,
            name => new Lazy<Task<PostgresGraphStore>>(() => this.CreateAndInitializeAsync(name)));

        return await lazy.Value.ConfigureAwait(false);
    }

    private async Task<PostgresGraphStore> CreateAndInitializeAsync(string graphName)
    {
        // Same wiring AddPostgresGraphStore performs: an options bag plus an AgeConnectionManager +
        // AgeClientFactory pair. Passing no factory lets PostgresGraphStore construct and OWN that
        // pair internally (identical construction), so the store disposes them when the provider does.
        var options = new PostgresGraphStoreOptions
        {
            ConnectionString = this.connectionString,
            GraphName = graphName,
        };

        var store = new PostgresGraphStore(options, this.loggerFactory.CreateLogger<PostgresGraphStore>(), this.loggerFactory);

        // ponytail: one-time graph creation is idempotent and fast; it is not tied to a caller's
        // CancellationToken because the initialized store is shared across all callers of this graph.
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
        return store;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var lazy in this.stores.Values)
        {
            if (!lazy.IsValueCreated)
            {
                continue;
            }

            try
            {
                var store = await lazy.Value.ConfigureAwait(false);
                await store.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best-effort disposal on shutdown.
            }
        }

        this.stores.Clear();
    }
}
