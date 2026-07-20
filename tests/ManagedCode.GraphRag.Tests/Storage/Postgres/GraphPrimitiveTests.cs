using System.Reflection;
using GraphRag.Storage.Postgres.ApacheAge.Types;
using GraphPath = GraphRag.Storage.Postgres.ApacheAge.Types.Path;

namespace ManagedCode.GraphRag.Tests.Storage.Postgres;

public sealed class GraphPrimitiveTests
{
    [Test]
    public async Task GraphId_ComparesAndFormats()
    {
        var a = new GraphId(10);
        var b = new GraphId(20);

        await Assert.That(a < b).IsTrue();
        await Assert.That(b > a).IsTrue();
        await Assert.That(a <= b).IsTrue();
        await Assert.That(b >= a).IsTrue();
        await Assert.That(a.ToString()).IsEqualTo("10");

        await Assert.That(a.CompareTo(a)).IsEqualTo(0);
        await Assert.That(a.CompareTo(b)).IsEqualTo(-1);
        await Assert.That(b.CompareTo(a)).IsEqualTo(1);

        await Assert.That(a == new GraphId(10)).IsTrue();
        await Assert.That(a != b).IsTrue();
        await Assert.That(() => a.CompareTo("not-a-graph-id")).Throws<ArgumentException>();
    }

    [Test]
    public async Task Vertex_ToStringIncludesProperties()
    {
        var vertex = new Vertex
        {
            Id = new GraphId(1),
            Label = "Entity",
            Properties = new Dictionary<string, object?> { ["name"] = "alpha" }
        };

        var representation = vertex.ToString();
        await Assert.That(representation).Contains(@"""label"": ""Entity""");
        await Assert.That(vertex).IsEqualTo(vertex);
        var clone = vertex;
        await Assert.That(vertex == clone).IsTrue();
        await Assert.That(vertex != clone).IsFalse();
    }

    [Test]
    public async Task Edge_ToStringIncludesEndpoints()
    {
        var edge = new Edge
        {
            Id = new GraphId(99),
            StartId = new GraphId(1),
            EndId = new GraphId(2),
            Label = "KNOWS",
            Properties = new Dictionary<string, object?> { ["weight"] = 0.5 }
        };

        var representation = edge.ToString();
        await Assert.That(representation).Contains(@"""start_id"": 1");
        await Assert.That(representation).Contains(@"""end_id"": 2");
        var clonedEdge = edge;
        await Assert.That(edge == clonedEdge).IsTrue();
        await Assert.That(edge != clonedEdge).IsFalse();
    }

    [Test]
    public async Task Path_ConstructsFromVertexAndEdgeSequence()
    {
        var vertices = new[]
        {
            new Vertex { Id = new GraphId(1), Label = "Person", Properties = new Dictionary<string, object?>() },
            new Vertex { Id = new GraphId(2), Label = "Person", Properties = new Dictionary<string, object?>() },
            new Vertex { Id = new GraphId(3), Label = "Person", Properties = new Dictionary<string, object?>() }
        };

        var edges = new[]
        {
            new Edge { Id = new GraphId(10), Label = "L", StartId = vertices[0].Id, EndId = vertices[1].Id, Properties = new Dictionary<string, object?>() },
            new Edge { Id = new GraphId(11), Label = "L", StartId = vertices[1].Id, EndId = vertices[2].Id, Properties = new Dictionary<string, object?>() }
        };

        var rawPath = new object[] { vertices[0], edges[0], vertices[1], edges[1], vertices[2] };
        var ctor = typeof(GraphPath).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(object[]) },
            modifiers: null);
        await Assert.That(ctor).IsNotNull();
        var path = (GraphPath)ctor!.Invoke(new object[] { rawPath });

        await Assert.That(path.Length).IsEqualTo(2);
        await Assert.That(path.Vertices.Length).IsEqualTo(3);
        await Assert.That(path.Edges.Length).IsEqualTo(2);
        await Assert.That(path.Vertices[^1].Id).IsEqualTo(vertices[2].Id);
        await Assert.That(path.Edges[^1].Id).IsEqualTo(edges[1].Id);
    }

    [Test]
    public async Task Path_InvalidSequenceThrows()
    {
        var rawPath = new object[] { new Edge() };
        var ctor = typeof(GraphPath).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(object[]) },
            modifiers: null);
        await Assert.That(ctor).IsNotNull();
        await Assert.That(() =>
        {
            try
            {
                ctor!.Invoke(new object[] { rawPath });
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }).Throws<FormatException>();
    }
}
