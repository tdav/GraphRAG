using System.Globalization;
using GraphRag.Storage.Postgres.ApacheAge.Types;

namespace ManagedCode.GraphRag.Tests.Storage.Postgres;

public sealed class AgtypeTests
{
    [Test]
    public async Task Agtype_ConvertsNumericValues()
    {
        var agtype = new Agtype("42");
        await Assert.That(agtype.GetString()).IsEqualTo("42");
        await Assert.That(agtype.GetInt32()).IsEqualTo(42);
        await Assert.That(agtype.GetUInt32()).IsEqualTo(42u);
        await Assert.That(agtype.GetInt64()).IsEqualTo(42L);
        await Assert.That(agtype.GetUInt64()).IsEqualTo(42UL);
        await Assert.That(agtype.GetDecimal()).IsEqualTo(42m);
        await Assert.That(agtype.GetByte()).IsEqualTo((byte)42);
        await Assert.That(agtype.GetSByte()).IsEqualTo((sbyte)42);
        await Assert.That(agtype.GetInt16()).IsEqualTo((short)42);
        await Assert.That(agtype.GetUInt16()).IsEqualTo((ushort)42);
    }

    [Test]
    public async Task Agtype_ConvertsFloatingPointLiterals()
    {
        await Assert.That(new Agtype("true").GetBoolean()).IsTrue();
        await Assert.That(new Agtype("false").GetBoolean()).IsFalse();

        await Assert.That(new Agtype("-Infinity").GetDouble()).IsEqualTo(double.NegativeInfinity);
        await Assert.That(new Agtype("Infinity").GetDouble()).IsEqualTo(double.PositiveInfinity);
        await Assert.That(double.IsNaN(new Agtype("NaN").GetDouble())).IsTrue();

        await Assert.That(new Agtype("-Infinity").GetFloat()).IsEqualTo(float.NegativeInfinity);
        await Assert.That(new Agtype("Infinity").GetFloat()).IsEqualTo(float.PositiveInfinity);
        await Assert.That(float.IsNaN(new Agtype("NaN").GetFloat())).IsTrue();
    }

    [Test]
    public async Task Agtype_ReturnsListsAndVertices()
    {
        var payload = @"[{""value"":1},{""value"":2}]";
        var list = new Agtype(payload).GetList();
        await Assert.That(list.Count).IsEqualTo(2);

        var vertexJson =
            @"{""id"": 1,""label"": ""Entity"",""properties"": {""name"": ""alpha""}}::vertex";
        var vertex = new Agtype(vertexJson).GetVertex();
        await Assert.That(vertex.Label).IsEqualTo("Entity");
        await Assert.That(vertex.Properties["name"]).IsEqualTo("alpha");

        var edgeJson =
            @"{""id"": 2,""label"": ""CONNECTS"",""start_id"": 1,""end_id"": 2,""properties"": {""weight"": 0.5}}::edge";
        var edge = new Agtype(edgeJson).GetEdge();
        await Assert.That(edge.Label).IsEqualTo("CONNECTS");
        await Assert.That(Convert.ToDecimal(edge.Properties["weight"], CultureInfo.InvariantCulture)).IsEqualTo(0.5m);
    }

    [Test]
    public async Task Agtype_ReturnsPaths()
    {
        var vertexA =
            @"{""id"": 1,""label"": ""Entity"",""properties"": {""name"": ""alpha""}}::vertex";
        var vertexB =
            @"{""id"": 2,""label"": ""Entity"",""properties"": {""name"": ""beta""}}::vertex";
        var edge =
            @"{""id"": 3,""label"": ""CONNECTS"",""start_id"": 1,""end_id"": 2,""properties"": {}}::edge";
        var pathPayload = $"[{vertexA},{edge},{vertexB}]::path";

        var path = new Agtype(pathPayload).GetPath();

        await Assert.That(path.Length).IsEqualTo(1);
        await Assert.That(path.Vertices.Length).IsEqualTo(2);
        await Assert.That(path.Edges).HasSingleItem();
        await Assert.That(path.Vertices[0].Properties["name"]).IsEqualTo("alpha");
        await Assert.That(path.Edges[0].Label).IsEqualTo("CONNECTS");
    }

    [Test]
    public async Task Agtype_InvalidVertexThrows()
    {
        var agtype = new Agtype(@"{""id"":1}::edge");
        await Assert.That(() => agtype.GetVertex()).Throws<FormatException>();
    }

    [Test]
    public async Task Agtype_InvalidPathThrows()
    {
        var agtype = new Agtype(@"[{""id"":1}]");
        await Assert.That(() => agtype.GetPath()).Throws<FormatException>();
    }
}
