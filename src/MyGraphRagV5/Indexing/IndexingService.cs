using GraphRag.Community;
using GraphRag.Config;
using GraphRag.Constants;
using GraphRag.Data;
using GraphRag.Entities;
using GraphRag.Graphs;
using GraphRag.Indexing;
using GraphRag.Relationships;
using GraphRag.Storage;
using GraphRag.Vectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGraphRagV5.Data;

namespace MyGraphRagV5.Indexing;

/// <summary>
/// Orchestrates a per-project indexing run: starts the GraphRAG pipeline in the background, then
/// syncs its output artifacts into the AGE graph (entities/relationships) and pgvector
/// (text units, community reports, entities). Tracks lifecycle in <see cref="RunRegistry"/> and the
/// <c>IndexingRun</c> table.
/// </summary>
public sealed class IndexingService(
    IServiceScopeFactory scopeFactory,
    RunRegistry registry,
    ProjectGraphStoreProvider graphStoreProvider,
    IVectorStore vectorStore,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IndexingPipelineRunner pipelineRunner,
    IHostEnvironment environment,
    ILogger<IndexingService> logger)
{
    private const string ChatModelId = "nemotron-3-super:cloud";
    private const string EmbedModelId = "tei-embed";

    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
    private readonly RunRegistry registry = registry;
    private readonly ProjectGraphStoreProvider graphStoreProvider = graphStoreProvider;
    private readonly IVectorStore vectorStore = vectorStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = embeddingGenerator;
    private readonly IndexingPipelineRunner pipelineRunner = pipelineRunner;
    private readonly IHostEnvironment environment = environment;
    private readonly ILogger<IndexingService> logger = logger;

    /// <summary>
    /// Starts indexing a project. Rejects the call if the project already has an active run.
    /// Returns the new run id immediately; the pipeline runs on a background task.
    /// </summary>
    public async Task<Guid> StartRunAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using var scope = this.scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = await db.RagProjects
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Project '{projectId}' was not found.");

        var runId = Guid.NewGuid();
        var runCts = new CancellationTokenSource();
        var handle = this.registry.TryRegister(runId, projectId, runCts);
        if (handle is null)
        {
            runCts.Dispose();
            throw new InvalidOperationException($"Project '{projectId}' already has an active indexing run.");
        }

        try
        {
            db.IndexingRuns.Add(new IndexingRun
            {
                Id = runId,
                ProjectId = projectId,
                Status = IndexingRunStatus.Running,
                StartedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            this.registry.Remove(runId);
            runCts.Dispose();
            throw;
        }

        // ponytail: fire-and-track via Task.Run — no hosted-service queue. If concurrency limits or
        // durable restart-after-crash are needed, replace with a Channel<Guid>-fed BackgroundService
        // that dequeues run ids; the run lifecycle in RunPipelineAsync stays unchanged.
        var task = Task.Run(() => this.RunPipelineAsync(runId, project, runCts), CancellationToken.None);
        handle.AttachTask(task);

        return runId;
    }

    /// <summary>Requests cancellation of an active run. Returns <c>false</c> if the run is unknown.</summary>
    public bool CancelRun(Guid runId) => this.registry.CancelRun(runId);

    private async Task RunPipelineAsync(Guid runId, RagProject project, CancellationTokenSource runCts)
    {
        var ct = runCts.Token;
        try
        {
            var config = BuildConfig(project);
            Directory.CreateDirectory(config.Output.BaseDir);

            await this.pipelineRunner
                .RunAsync(config, new RunProgressCallbacks(runId, this.registry), ct)
                .ConfigureAwait(false);

            var outputStorage = PipelineStorageFactory.Create(config.Output);
            await this.SyncGraphAsync(project, outputStorage, ct).ConfigureAwait(false);
            var dimension = await this.SyncEmbeddingsAsync(project, outputStorage, ct).ConfigureAwait(false);
            var documentCount = await CountDocumentsAsync(outputStorage, ct).ConfigureAwait(false);

            if (dimension is { } dim)
            {
                await this.TrySetEmbeddingDimensionAsync(project.Id, dim).ConfigureAwait(false);
            }

            await this.UpdateRunAsync(runId, IndexingRunStatus.Succeeded, DateTimeOffset.UtcNow, documentCount, error: null)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await this.UpdateRunAsync(runId, IndexingRunStatus.Cancelled, DateTimeOffset.UtcNow, documentCount: null, error: null)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Indexing run {RunId} for project {ProjectId} failed.", runId, project.Id);
            await this.UpdateRunAsync(runId, IndexingRunStatus.Failed, DateTimeOffset.UtcNow, documentCount: null, error: ex.Message)
                .ConfigureAwait(false);
        }
        finally
        {
            this.registry.Remove(runId);
            runCts.Dispose();
        }
    }

    private GraphRagConfig BuildConfig(RagProject project)
    {
        var outputDir = Path.Combine(this.environment.ContentRootPath, "data", "output", project.Id.ToString());

        var config = new GraphRagConfig();
        config.Models.Add(ChatModelId);

        config.Input.Storage.Type = StorageType.File;
        config.Input.Storage.BaseDir = project.SourceFolder;
        config.Input.FileType = InputFileType.Text;
        config.Input.FilePattern = project.FilePattern;

        config.Output.Type = StorageType.File;
        config.Output.BaseDir = outputDir;

        // Matches the keyed TEI embedding registration (Task 4); the pipeline's unkeyed fallback also
        // resolves it. The chat model above drives entity/community summarization.
        config.EmbedText.ModelId = EmbedModelId;

        return config;
    }

    private async Task SyncGraphAsync(RagProject project, IPipelineStorage outputStorage, CancellationToken ct)
    {
        var entities = await LoadOrEmptyAsync<EntityRecord>(outputStorage, PipelineTableNames.Entities, ct).ConfigureAwait(false);
        var relationships = await LoadOrEmptyAsync<RelationshipRecord>(outputStorage, PipelineTableNames.Relationships, ct).ConfigureAwait(false);

        var store = await this.graphStoreProvider.GetStoreAsync(project.GraphName, ct).ConfigureAwait(false);

        if (entities.Count > 0)
        {
            await store.UpsertNodesAsync(entities.Select(ToNode).ToList(), ct).ConfigureAwait(false);
        }

        if (relationships.Count > 0)
        {
            await store.UpsertRelationshipsAsync(relationships.Select(ToRelationship).ToList(), ct).ConfigureAwait(false);
        }
    }

    private async Task<int?> SyncEmbeddingsAsync(RagProject project, IPipelineStorage outputStorage, CancellationToken ct)
    {
        var textUnits = await LoadOrEmptyAsync<TextUnitRecord>(outputStorage, PipelineTableNames.TextUnits, ct).ConfigureAwait(false);
        var reports = await LoadOrEmptyAsync<CommunityReportRecord>(outputStorage, PipelineTableNames.CommunityReports, ct).ConfigureAwait(false);
        var entities = await LoadOrEmptyAsync<EntityRecord>(outputStorage, PipelineTableNames.Entities, ct).ConfigureAwait(false);

        var items = new List<EmbeddingItem>(textUnits.Count + reports.Count + entities.Count);
        items.AddRange(textUnits.Where(u => !string.IsNullOrWhiteSpace(u.Text)).Select(TextUnitEmbedding));
        items.AddRange(reports.Where(r => !string.IsNullOrWhiteSpace(r.Summary)).Select(CommunityEmbedding));
        items.AddRange(entities.Select(EntityEmbedding));

        if (items.Count == 0)
        {
            return null;
        }

        var embeddings = await this.embeddingGenerator
            .GenerateAsync(items.Select(i => i.Text), cancellationToken: ct)
            .ConfigureAwait(false);

        for (var i = 0; i < items.Count && i < embeddings.Count; i++)
        {
            await this.vectorStore
                .UpsertAsync(project.VectorCollection, embeddings[i].Vector, items[i].Metadata, ct)
                .ConfigureAwait(false);
        }

        return embeddings.Count > 0 ? embeddings[0].Vector.Length : null;
    }

    private static async Task<int> CountDocumentsAsync(IPipelineStorage outputStorage, CancellationToken ct)
    {
        var documents = await LoadOrEmptyAsync<DocumentProbe>(outputStorage, PipelineTableNames.Documents, ct).ConfigureAwait(false);
        return documents.Count;
    }

    private async Task UpdateRunAsync(Guid runId, IndexingRunStatus status, DateTimeOffset? completedAt, int? documentCount, string? error)
    {
        using var scope = this.scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // IndexingRun uses init-only properties; ExecuteUpdate issues a single SQL UPDATE without
        // materializing/mutating the entity. CancellationToken.None: a finished run must persist even
        // when the run token has been cancelled.
        await db.IndexingRuns
            .Where(r => r.Id == runId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.Status, status)
                    .SetProperty(r => r.CompletedAt, completedAt)
                    .SetProperty(r => r.DocumentCount, documentCount)
                    .SetProperty(r => r.Error, error),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private async Task TrySetEmbeddingDimensionAsync(Guid projectId, int dimension)
    {
        using var scope = this.scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.RagProjects
            .Where(p => p.Id == projectId && p.EmbeddingDimension == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.EmbeddingDimension, dimension), CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<T>> LoadOrEmptyAsync<T>(IPipelineStorage storage, string name, CancellationToken ct)
    {
        try
        {
            return await storage.LoadTableAsync<T>(name, ct).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            // A run may legitimately produce no rows for a table; treat a missing artifact as empty.
            return Array.Empty<T>();
        }
    }

    // --- Pure artifact-to-store mappings (internal for unit testing) ---

    /// <summary>
    /// Entity → graph node. Node id is the entity <see cref="EntityRecord.Title"/> (its name), because
    /// relationships reference their endpoints by name (<see cref="RelationshipRecord.Source"/>/Target),
    /// so name-as-id keeps edges connected to their vertices.
    /// </summary>
    internal static GraphNodeUpsert ToNode(EntityRecord e)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["entityId"] = e.Id,
            ["type"] = e.Type,
            ["description"] = e.Description,
            ["frequency"] = e.Frequency,
            ["degree"] = e.Degree,
            ["humanReadableId"] = e.HumanReadableId,
        };

        return new GraphNodeUpsert(e.Title, "Entity", properties);
    }

    internal static GraphRelationshipUpsert ToRelationship(RelationshipRecord r)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["relationshipId"] = r.Id,
            ["description"] = r.Description,
            ["weight"] = r.Weight,
            ["combinedDegree"] = r.CombinedDegree,
            ["originalType"] = r.Type,
        };

        return new GraphRelationshipUpsert(r.Source, r.Target, NormalizeRelationshipType(r.Type), properties, r.Bidirectional);
    }

    /// <summary>
    /// Sanitizes an LLM-produced relationship type into a valid AGE edge label (letters/digits/
    /// underscore, not starting with a digit). Trust boundary: the type flows into a Cypher label.
    /// </summary>
    internal static string NormalizeRelationshipType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "RELATED_TO";
        }

        var cleaned = new string(type.Trim().Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        return char.IsDigit(cleaned[0]) ? "_" + cleaned : cleaned;
    }

    internal static EmbeddingItem TextUnitEmbedding(TextUnitRecord u) =>
        new(u.Text, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = $"text_unit:{u.Id}",
            ["text"] = u.Text,
            ["type"] = "text_unit",
            ["title"] = u.Id,
        });

    internal static EmbeddingItem CommunityEmbedding(CommunityReportRecord c) =>
        new(c.Summary, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = $"community:{c.CommunityId}",
            ["text"] = c.Summary,
            ["type"] = "community",
            ["title"] = $"Community {c.CommunityId} (level {c.Level})",
        });

    internal static EmbeddingItem EntityEmbedding(EntityRecord e)
    {
        var text = string.IsNullOrWhiteSpace(e.Description) ? e.Title : $"{e.Title}. {e.Description}";
        return new EmbeddingItem(text, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = $"entity:{e.Title}",
            ["text"] = text,
            ["type"] = "entity",
            ["title"] = e.Title,
        });
    }

    /// <summary>Text to embed plus the vector-store metadata for one artifact.</summary>
    internal sealed record EmbeddingItem(string Text, IReadOnlyDictionary<string, object?> Metadata);

    /// <summary>Minimal shape used only to count rows in the documents artifact.</summary>
    private sealed record DocumentProbe(string? Id);
}
