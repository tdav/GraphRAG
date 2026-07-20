using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GraphRag.Storage.Postgres.ApacheAge.JsonConverters;
using GraphRag.Storage.Postgres.ApacheAge.Types;

namespace ManagedCode.GraphRag.Tests.Storage.Postgres;

public sealed class AgeJsonConverterTests
{
    [Test]
    public async Task GraphIdConverter_ReadsAndWrites()
    {
        var converter = new GraphIdConverter();
        var json = JsonDocument.Parse("123").RootElement.GetRawText();
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        reader.Read();
        var graphId = converter.Read(ref reader, typeof(GraphId), new JsonSerializerOptions());
        await Assert.That(graphId.Value).IsEqualTo((ulong)123);

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        converter.Write(writer, graphId, new JsonSerializerOptions());
        writer.Flush();
        await Assert.That(Encoding.UTF8.GetString(stream.ToArray())).IsEqualTo("123");
    }

    [Test]
    public async Task InferredObjectConverter_ParsesTokens()
    {
        var converter = new InferredObjectConverter();
        var options = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

        object Read(string payload)
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(payload));
            reader.Read();
            return converter.Read(ref reader, typeof(object), options)!;
        }

        var smallInt = Read("5");
        await Assert.That(smallInt).IsTypeOf<int>();
        await Assert.That((int)smallInt).IsEqualTo(5);

        var largeInt = Read("5000000000");
        await Assert.That(largeInt).IsTypeOf<long>();
        await Assert.That((long)largeInt).IsEqualTo(5000000000L);

        var decimalValue = Read("5.5");
        await Assert.That(decimalValue).IsTypeOf<decimal>();
        await Assert.That((decimal)decimalValue).IsEqualTo(5.5m);

        await Assert.That((bool)Read("true")).IsTrue();
        await Assert.That(Read(@"""text""")).IsEqualTo("text");
        await Assert.That(double.IsPositiveInfinity((double)Read(@"""Infinity"""))).IsTrue();
        var array = (List<object?>)Read("[1,2,3]");
        await Assert.That(array.Count).IsEqualTo(3);
    }

    [Test]
    public async Task PathObjectConverter_AlternatesVerticesAndEdges()
    {
        var converter = new PathObjectConverter();
        var vertex = @"{""id"":1,""label"":""Node"",""properties"":{}}";
        var edge = @"{""id"":2,""label"":""LINKS_TO"",""start_id"":1,""end_id"":2,""properties"":{}}";

        object Deserialize(string payload)
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(payload));
            reader.Read();
            return converter.Read(ref reader, typeof(object), SerializerOptions.Default)!;
        }

        await Assert.That(Deserialize(vertex)).IsTypeOf<Vertex>();
        await Assert.That(Deserialize(edge)).IsTypeOf<Edge>();
        await Assert.That(Deserialize(vertex)).IsTypeOf<Vertex>();
    }

    [Test]
    public async Task SerializerOptions_ConfiguresConverters()
    {
        await Assert.That(SerializerOptions.Default.Converters).Contains(converter => converter is InferredObjectConverter);
        await Assert.That(SerializerOptions.Default.Converters).Contains(converter => converter is GraphIdConverter);
        await Assert.That(SerializerOptions.PathSerializer.Converters).Contains(converter => converter is PathObjectConverter);
    }
}
