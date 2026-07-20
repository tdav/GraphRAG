using System.Text;
using GraphRag.Graphs;
using GraphRag.Storage.Postgres;
using ManagedCode.GraphRag.Tests.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManagedCode.GraphRag.Tests.Storage.Postgres;

[ClassDataSource<GraphRagApplicationFixture>(Shared = SharedType.PerAssembly)]
public sealed class PostgresDiagnosticTests(GraphRagApplicationFixture fixture)
{
    [Test]
    public async Task PostgresExplainService_ReturnsPlanOutput()
    {
        var store = fixture.Services.GetKeyedService<PostgresGraphStore>("postgres");
        if (store is null)
        {
            return;
        }

        await store.InitializeAsync();

        var nodeId = $"plan-{Guid.NewGuid():N}";
        await store.UpsertNodeAsync(nodeId, "Person", new Dictionary<string, object?> { ["name"] = "Planner" });

        var service = new PostgresExplainService(store, NullLogger<PostgresExplainService>.Instance);
        var plan = await service.GetFormattedPlanAsync("MATCH (n:Person) RETURN n LIMIT 1");

        await Assert.That(plan).Contains("EXPLAIN plan:");
        await Assert.That(plan.Contains("Person", StringComparison.OrdinalIgnoreCase)).IsTrue();

        using var writer = new StringWriter();
        await service.WritePlanAsync("MATCH (n:Person) RETURN n LIMIT 1", writer, cancellationToken: default);
        await Assert.That(string.IsNullOrWhiteSpace(writer.ToString())).IsFalse();
    }

    [Test]
    public async Task PostgresIngestionBenchmark_UpsertsNodesAndRelationships()
    {
        var store = fixture.Services.GetKeyedService<PostgresGraphStore>("postgres");
        if (store is null)
        {
            return;
        }

        await store.InitializeAsync();

        var prefix = Guid.NewGuid().ToString("N");
        var csv = $"source_id,target_id,weight{Environment.NewLine}{prefix}-100,{prefix}-200,0.5{Environment.NewLine}{prefix}-100,{prefix}-300,0.7{Environment.NewLine}";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var benchmark = new PostgresGraphIngestionBenchmark(store, NullLogger<PostgresGraphIngestionBenchmark>.Instance);
        var options = new PostgresIngestionBenchmarkOptions
        {
            EdgeLabel = "KNOWS",
            SourceLabel = "Person",
            TargetLabel = "Person"
        };
        options.EdgePropertyColumns["weight"] = "weight";

        var result = await benchmark.RunAsync(stream, options);

        await Assert.That(result.NodesWritten).IsEqualTo(3);
        await Assert.That(result.RelationshipsWritten).IsEqualTo(2);

        var relationships = new List<GraphRelationship>();
        await foreach (var relationship in store.GetOutgoingRelationshipsAsync($"{prefix}-100"))
        {
            relationships.Add(relationship);
        }

        await Assert.That(relationships.Count).IsEqualTo(2);
        foreach (var rel in relationships)
        {
            await Assert.That(rel.Type).IsEqualTo("KNOWS");
        }
    }
}
