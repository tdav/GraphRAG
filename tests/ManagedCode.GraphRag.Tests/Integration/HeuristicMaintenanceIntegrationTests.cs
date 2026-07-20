using System.Collections.Immutable;
using GraphRag;
using GraphRag.Callbacks;
using GraphRag.Community;
using GraphRag.Config;
using GraphRag.Constants;
using GraphRag.Data;
using GraphRag.Entities;
using GraphRag.Indexing.Runtime;
using GraphRag.Indexing.Workflows;
using GraphRag.Relationships;
using GraphRag.Storage;
using ManagedCode.GraphRag.Tests.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.GraphRag.Tests.Integration;

public sealed class HeuristicMaintenanceIntegrationTests : IDisposable
{
    private readonly string _rootDir;

    public HeuristicMaintenanceIntegrationTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "GraphRag", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    [Test]
    public async Task HeuristicMaintenanceWorkflow_AppliesBudgetsAndSemanticDeduplication()
    {
        var outputDir = PrepareDirectory("output-maintenance");
        var inputDir = PrepareDirectory("input-maintenance");
        var previousDir = PrepareDirectory("previous-maintenance");

        var textUnits = new[]
        {
            new TextUnitRecord
            {
                Id = "a",
                Text = "Alpha Beta",
                TokenCount = 40,
                DocumentIds = new[] { "doc-1" },
                EntityIds = Array.Empty<string>(),
                RelationshipIds = Array.Empty<string>(),
                CovariateIds = Array.Empty<string>()
            },
            new TextUnitRecord
            {
                Id = "b",
                Text = "Gamma Delta",
                TokenCount = 30,
                DocumentIds = new[] { "doc-1" },
                EntityIds = Array.Empty<string>(),
                RelationshipIds = Array.Empty<string>(),
                CovariateIds = Array.Empty<string>()
            },
            new TextUnitRecord
            {
                Id = "c",
                Text = "Trim me",
                TokenCount = 30,
                DocumentIds = new[] { "doc-1" },
                EntityIds = Array.Empty<string>(),
                RelationshipIds = Array.Empty<string>(),
                CovariateIds = Array.Empty<string>()
            },
            new TextUnitRecord
            {
                Id = "d",
                Text = "Alpha Beta",
                TokenCount = 35,
                DocumentIds = new[] { "doc-2" },
                EntityIds = Array.Empty<string>(),
                RelationshipIds = Array.Empty<string>(),
                CovariateIds = Array.Empty<string>()
            }
        };

        var outputStorage = new FilePipelineStorage(outputDir);
        await outputStorage.WriteTableAsync(PipelineTableNames.TextUnits, textUnits);

        var embeddingVectors = new Dictionary<string, float[]>
        {
            ["Alpha Beta"] = new[] { 1f, 0f },
            ["Gamma Delta"] = new[] { 0f, 1f }
        };

        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IChatClient>(new TestChatClientFactory().CreateClient())
            .AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(new StubEmbeddingGenerator(embeddingVectors))
            .AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>("dedupe-model", (sp, _) => sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>())
            .AddGraphRag()
            .BuildServiceProvider();

        var config = new GraphRagConfig
        {
            Heuristics = new HeuristicMaintenanceConfig
            {
                MaxTokensPerTextUnit = 50,
                MaxDocumentTokenBudget = 80,
                EnableSemanticDeduplication = true,
                SemanticDeduplicationThreshold = 0.75,
                EmbeddingModelId = "dedupe-model"
            }
        };

        var context = new PipelineRunContext(
            inputStorage: new FilePipelineStorage(inputDir),
            outputStorage: outputStorage,
            previousStorage: new FilePipelineStorage(previousDir),
            cache: new StubPipelineCache(),
            callbacks: NoopWorkflowCallbacks.Instance,
            stats: new PipelineRunStats(),
            state: new PipelineState(),
            services: services);

        var workflow = HeuristicMaintenanceWorkflow.Create();
        await workflow(config, context, CancellationToken.None);

        var processed = await outputStorage.LoadTableAsync<TextUnitRecord>(PipelineTableNames.TextUnits);
        await Assert.That(processed.Count).IsEqualTo(2);

        await Assert.That(processed).HasSingleItem(unit => unit.Id == "a");
        var merged = processed.Single(unit => unit.Id == "a");
        await Assert.That(merged.DocumentIds.Count).IsEqualTo(2);
        await Assert.That(merged.DocumentIds).Contains("doc-1", StringComparer.OrdinalIgnoreCase);
        await Assert.That(merged.DocumentIds).Contains("doc-2", StringComparer.OrdinalIgnoreCase);
        await Assert.That(merged.TokenCount).IsEqualTo(75);

        await Assert.That(processed).HasSingleItem(unit => unit.Id == "b");
        var survivor = processed.Single(unit => unit.Id == "b");
        await Assert.That(survivor.DocumentIds).HasSingleItem();
        await Assert.That(survivor.DocumentIds[0]).IsEqualTo("doc-1");
        await Assert.That(processed).DoesNotContain(unit => unit.Id == "c");
        await Assert.That(processed).DoesNotContain(unit => unit.Id == "d" && unit.DocumentIds.Count == 1);
    }

    [Test]
    public async Task ExtractGraphWorkflow_LinksOrphansAndEnforcesRelationshipFloors()
    {
        var outputDir = PrepareDirectory("output-graph");
        var inputDir = PrepareDirectory("input-graph");
        var previousDir = PrepareDirectory("previous-graph");

        var outputStorage = new FilePipelineStorage(outputDir);
        await outputStorage.WriteTableAsync(PipelineTableNames.TextUnits, new[]
        {
            new TextUnitRecord
            {
                Id = "unit-1",
                Text = "Alice collaborates with Bob on research.",
                TokenCount = 12,
                DocumentIds = new[] { "doc-1" },
                EntityIds = Array.Empty<string>(),
                RelationshipIds = Array.Empty<string>(),
                CovariateIds = Array.Empty<string>()
            },
            new TextUnitRecord
            {
                Id = "unit-2",
                Text = "Charlie and Alice planned a workshop.",
                TokenCount = 18,
                DocumentIds = new[] { "doc-1" },
                EntityIds = Array.Empty<string>(),
                RelationshipIds = Array.Empty<string>(),
                CovariateIds = Array.Empty<string>()
            }
        });

        var responses = new Queue<string>(new[]
        {
            "{\"entities\": [ { \"title\": \"Alice\", \"type\": \"person\", \"description\": \"Researcher\", \"confidence\": 0.9 }, { \"title\": \"Bob\", \"type\": \"person\", \"description\": \"Engineer\", \"confidence\": 0.6 } ], \"relationships\": [ { \"source\": \"Alice\", \"target\": \"Bob\", \"type\": \"collaborates\", \"description\": \"Works together\", \"weight\": 0.1, \"bidirectional\": false } ] }",
            "{\"entities\": [ { \"title\": \"Alice\", \"type\": \"person\", \"description\": \"Researcher\", \"confidence\": 0.8 }, { \"title\": \"Charlie\", \"type\": \"person\", \"description\": \"Analyst\", \"confidence\": 0.7 } ], \"relationships\": [] }"
        });

        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IChatClient>(new TestChatClientFactory(_ =>
            {
                if (responses.Count == 0)
                {
                    throw new InvalidOperationException("No chat responses remaining.");
                }

                var payload = responses.Dequeue();
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, payload));
            }).CreateClient())
            .AddGraphRag()
            .BuildServiceProvider();

        var config = new GraphRagConfig
        {
            Heuristics = new HeuristicMaintenanceConfig
            {
                LinkOrphanEntities = true,
                OrphanLinkWeight = 0.5,
                MaxTextUnitsPerRelationship = 1,
                RelationshipConfidenceFloor = 0.4
            }
        };

        var context = new PipelineRunContext(
            inputStorage: new FilePipelineStorage(inputDir),
            outputStorage: outputStorage,
            previousStorage: new FilePipelineStorage(previousDir),
            cache: new StubPipelineCache(),
            callbacks: NoopWorkflowCallbacks.Instance,
            stats: new PipelineRunStats(),
            state: new PipelineState(),
            services: services);

        var workflow = ExtractGraphWorkflow.Create();
        await workflow(config, context, CancellationToken.None);

        var relationships = await outputStorage.LoadTableAsync<RelationshipRecord>(PipelineTableNames.Relationships);
        await Assert.That(relationships.Count).IsEqualTo(2);

        await Assert.That(relationships).HasSingleItem(rel => rel.Source == "Alice" && rel.Target == "Bob");
        var direct = relationships.Single(rel => rel.Source == "Alice" && rel.Target == "Bob");
        await Assert.That(direct.Weight).IsEqualTo(0.4).Within(0.0005);
        await Assert.That(direct.TextUnitIds).Contains("unit-1");
        await Assert.That(direct.Bidirectional).IsFalse();

        await Assert.That(relationships).HasSingleItem(rel => rel.Source == "Charlie" && rel.Target == "Alice");
        var synthetic = relationships.Single(rel => rel.Source == "Charlie" && rel.Target == "Alice");
        await Assert.That(synthetic.Bidirectional).IsTrue();
        await Assert.That(synthetic.Weight).IsEqualTo(0.5).Within(0.0005);
        await Assert.That(synthetic.TextUnitIds).HasSingleItem();
        var orphanUnit = synthetic.TextUnitIds.Single();
        await Assert.That(orphanUnit).IsEqualTo("unit-2");

        var entities = await outputStorage.LoadTableAsync<EntityRecord>(PipelineTableNames.Entities);
        await Assert.That(entities.Count).IsEqualTo(3);
        await Assert.That(entities).Contains(entity => entity.Title == "Charlie");
    }

    [Test]
    public async Task CreateCommunitiesWorkflow_UsesFastLabelPropagationAssignments()
    {
        var outputDir = PrepareDirectory("output-communities");
        var inputDir = PrepareDirectory("input-communities");
        var previousDir = PrepareDirectory("previous-communities");

        var outputStorage = new FilePipelineStorage(outputDir);

        var entities = new[]
        {
            new EntityRecord("entity-1", 0, "Alice", "Person", "Researcher", ImmutableArray.Create("unit-1"), 2, 2, 0, 0),
            new EntityRecord("entity-2", 1, "Bob", "Person", "Engineer", ImmutableArray.Create("unit-1"), 2, 2, 0, 0),
            new EntityRecord("entity-3", 2, "Charlie", "Person", "Analyst", ImmutableArray.Create("unit-2"), 2, 1, 0, 0),
            new EntityRecord("entity-4", 3, "Diana", "Person", "Strategist", ImmutableArray.Create("unit-3"), 2, 1, 0, 0),
            new EntityRecord("entity-5", 4, "Eve", "Person", "Planner", ImmutableArray.Create("unit-3"), 2, 1, 0, 0)
        };

        await outputStorage.WriteTableAsync(PipelineTableNames.Entities, entities);

        var relationships = new[]
        {
            new RelationshipRecord("rel-1", 0, "Alice", "Bob", "collaborates", "", 0.9, 2, ImmutableArray.Create("unit-1"), true),
            new RelationshipRecord("rel-2", 1, "Bob", "Charlie", "supports", "", 0.85, 2, ImmutableArray.Create("unit-2"), true),
            new RelationshipRecord("rel-3", 2, "Diana", "Eve", "partners", "", 0.95, 2, ImmutableArray.Create("unit-3"), true)
        };

        await outputStorage.WriteTableAsync(PipelineTableNames.Relationships, relationships);

        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IChatClient>(new TestChatClientFactory().CreateClient())
            .AddGraphRag()
            .BuildServiceProvider();

        var config = new GraphRagConfig
        {
            ClusterGraph = new ClusterGraphConfig
            {
                Algorithm = CommunityDetectionAlgorithm.FastLabelPropagation,
                MaxIterations = 8,
                MaxClusterSize = 10,
                Seed = 13,
                UseLargestConnectedComponent = false
            }
        };

        var context = new PipelineRunContext(
            inputStorage: new FilePipelineStorage(inputDir),
            outputStorage: outputStorage,
            previousStorage: new FilePipelineStorage(previousDir),
            cache: new StubPipelineCache(),
            callbacks: NoopWorkflowCallbacks.Instance,
            stats: new PipelineRunStats(),
            state: new PipelineState(),
            services: services);

        var workflow = CreateCommunitiesWorkflow.Create();
        await workflow(config, context, CancellationToken.None);

        var communities = await outputStorage.LoadTableAsync<CommunityRecord>(PipelineTableNames.Communities);
        await Assert.That(communities.Count).IsEqualTo(2);
        await Assert.That(context.Items["create_communities:count"]).IsOfType(typeof(int));
        await Assert.That((int)context.Items["create_communities:count"]!).IsEqualTo(communities.Count);

        var titleLookup = entities.ToDictionary(entity => entity.Id, entity => entity.Title, StringComparer.OrdinalIgnoreCase);

        var members = communities
            .Select(community => community.EntityIds
                .Select(id => titleLookup[id])
                .OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
                .ToArray())
            .ToList();

        await Assert.That(members).Contains(group => group.SequenceEqual(new[] { "Alice", "Bob", "Charlie" }));
        await Assert.That(members).Contains(group => group.SequenceEqual(new[] { "Diana", "Eve" }));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDir))
            {
                Directory.Delete(_rootDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests.
        }
    }

    private string PrepareDirectory(string name)
    {
        var path = Path.Combine(_rootDir, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
