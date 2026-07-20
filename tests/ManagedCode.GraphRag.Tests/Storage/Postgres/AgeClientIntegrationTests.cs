using GraphRag.Storage.Postgres.ApacheAge;
using GraphRag.Storage.Postgres.ApacheAge.Types;
using ManagedCode.GraphRag.Tests.Integration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManagedCode.GraphRag.Tests.Storage.Postgres;

[ClassDataSource<GraphRagApplicationFixture>(Shared = SharedType.PerAssembly)]
public sealed class AgeClientIntegrationTests(GraphRagApplicationFixture fixture)
{
    [Test]
    public async Task AgeClient_RoundTripsVerticesWithAgtypeReader()
    {
        var connectionString = fixture.PostgresConnectionString;
        await using var manager = new AgeConnectionManager(connectionString, NullLogger<AgeConnectionManager>.Instance);
        await using var client = new AgeClient(manager, NullLogger<AgeClient>.Instance);

        await client.OpenConnectionAsync();
        var graphName = $"agetype_{Guid.NewGuid():N}";
        await client.CreateGraphAsync(graphName);
        var nodeId = $"node-{Guid.NewGuid():N}";

        await client.ExecuteCypherAsync(
            graphName,
            $"CREATE (:Entity {{ id: '{nodeId}', score: 42 }})");

        var query = $"SELECT * FROM ag_catalog.cypher('{graphName}', $$ MATCH (n:Entity {{ id: '{nodeId}' }}) RETURN n $$) AS (vertex ag_catalog.agtype);";
        var reader = await client.ExecuteQueryAsync(query);
        await Assert.That(await reader.ReadAsync()).IsTrue();

        var buffer = new object[reader.FieldCount];
        reader.GetValues(buffer);

        var directAgtype = (Agtype)buffer[0];
        var vertex = directAgtype.GetVertex();
        await Assert.That(vertex.Properties["id"]).IsEqualTo(nodeId);

        var viaTyped = reader.GetValue<Agtype>(0);
        await Assert.That(viaTyped.GetVertex().Label).IsEqualTo(vertex.Label);

        var viaAsync = await reader.GetValueAsync<Agtype>(0);
        await Assert.That(viaAsync.GetVertex().Properties["score"]).IsEqualTo(vertex.Properties["score"]);

        await reader.DisposeAsync();

        await client.DropGraphAsync(graphName, cascade: true);
        await Assert.That(await client.GraphExistsAsync(graphName)).IsFalse();
        await client.CloseConnectionAsync();
    }
}
