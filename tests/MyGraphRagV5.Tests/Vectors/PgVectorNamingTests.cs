using System.Text.Json;
using MyGraphRagV5.Vectors;

namespace MyGraphRagV5.Tests.Vectors;

public class PgVectorNamingTests
{
    [Test]
    [Arguments("MyCollection", "mycollection")]
    [Arguments("my collection", "my_collection")]
    [Arguments("my-collection!", "my_collection_")]
    [Arguments("already_ok_123", "already_ok_123")]
    [Arguments("Über Café", "_ber_caf_")]
    public async Task SanitizeCollectionName_ProducesLowercaseAsciiIdentifier(string input, string expected)
    {
        var sanitized = PgVectorNaming.SanitizeCollectionName(input);

        await Assert.That(sanitized).IsEqualTo(expected);
        await Assert.That(sanitized).Matches("^[a-z0-9_]+$");
    }

    [Test]
    public async Task SanitizeCollectionName_IsStableAcrossCalls()
    {
        var first = PgVectorNaming.SanitizeCollectionName("Some Collection");
        var second = PgVectorNaming.SanitizeCollectionName("Some Collection");

        await Assert.That(second).IsEqualTo(first);
    }

    [Test]
    public async Task SanitizeCollectionName_RejectsNull()
    {
        await Assert.That(() => PgVectorNaming.SanitizeCollectionName(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task SanitizeCollectionName_RejectsEmptyOrWhitespace(string input)
    {
        await Assert.That(() => PgVectorNaming.SanitizeCollectionName(input)).Throws<ArgumentException>();
    }

    [Test]
    public async Task TableName_PrefixesSanitizedNameWithVec()
    {
        var table = PgVectorNaming.TableName("My Docs");

        await Assert.That(table).IsEqualTo("vec_my_docs");
    }

    [Test]
    public async Task ResolveId_UsesMetadataIdWhenPresent()
    {
        var metadata = new Dictionary<string, object?> { ["id"] = "chunk-42" };

        await Assert.That(PgVectorNaming.ResolveId(metadata)).IsEqualTo("chunk-42");
    }

    [Test]
    public async Task ResolveId_StringifiesNonStringIdValues()
    {
        var metadata = new Dictionary<string, object?> { ["id"] = 42 };

        await Assert.That(PgVectorNaming.ResolveId(metadata)).IsEqualTo("42");
    }

    [Test]
    public async Task ResolveId_GeneratesGuidWhenIdMissing()
    {
        var metadata = new Dictionary<string, object?>();

        var id = PgVectorNaming.ResolveId(metadata);

        await Assert.That(Guid.TryParse(id, out _)).IsTrue();
    }

    [Test]
    public async Task ResolveId_GeneratesGuidWhenIdIsNull()
    {
        var metadata = new Dictionary<string, object?> { ["id"] = null };

        var id = PgVectorNaming.ResolveId(metadata);

        await Assert.That(Guid.TryParse(id, out _)).IsTrue();
    }

    [Test]
    public async Task ResolveText_ReturnsTextWhenPresent()
    {
        var metadata = new Dictionary<string, object?> { ["text"] = "chunk body" };

        await Assert.That(PgVectorNaming.ResolveText(metadata)).IsEqualTo("chunk body");
    }

    [Test]
    public async Task ResolveText_ReturnsNullWhenMissing()
    {
        var metadata = new Dictionary<string, object?>();

        await Assert.That(PgVectorNaming.ResolveText(metadata)).IsNull();
    }

    [Test]
    public async Task JsonElementToClr_ConvertsStringToString()
    {
        var element = ParseElement("\"hello\"");

        await Assert.That(PgVectorNaming.JsonElementToClr(element)).IsEqualTo("hello");
    }

    [Test]
    public async Task JsonElementToClr_ConvertsIntegralNumberToLong()
    {
        var element = ParseElement("42");

        var result = PgVectorNaming.JsonElementToClr(element);

        await Assert.That(result).IsTypeOf<long>();
        await Assert.That(result).IsEqualTo(42L);
    }

    [Test]
    public async Task JsonElementToClr_ConvertsFractionalNumberToDouble()
    {
        var element = ParseElement("3.14");

        var result = PgVectorNaming.JsonElementToClr(element);

        await Assert.That(result).IsTypeOf<double>();
        await Assert.That(result).IsEqualTo(3.14);
    }

    [Test]
    [Arguments("true", true)]
    [Arguments("false", false)]
    public async Task JsonElementToClr_ConvertsBooleans(string json, bool expected)
    {
        var element = ParseElement(json);

        await Assert.That(PgVectorNaming.JsonElementToClr(element)).IsEqualTo(expected);
    }

    [Test]
    public async Task JsonElementToClr_ConvertsNullToNull()
    {
        var element = ParseElement("null");

        await Assert.That(PgVectorNaming.JsonElementToClr(element)).IsNull();
    }

    [Test]
    public async Task JsonElementToClr_ConvertsNestedObjectToDictionary()
    {
        var element = ParseElement("""{"inner": "value", "count": 2}""");

        var converted = PgVectorNaming.JsonElementToClr(element);
        await Assert.That(converted).IsAssignableTo<IReadOnlyDictionary<string, object?>>();
        var result = (IReadOnlyDictionary<string, object?>)converted!;

        await Assert.That(result["inner"]).IsEqualTo("value");
        await Assert.That(result["count"]).IsEqualTo(2L);
    }

    [Test]
    public async Task JsonElementToClr_ConvertsArrayToListOfConvertedElements()
    {
        var element = ParseElement("[1, \"two\", true, null]");

        var converted = PgVectorNaming.JsonElementToClr(element);
        await Assert.That(converted).IsAssignableTo<List<object?>>();
        var result = (List<object?>)converted!;

        await Assert.That(result).IsEquivalentTo(new object?[] { 1L, "two", true, null });
    }

    private static JsonElement ParseElement(string json) => JsonDocument.Parse(json).RootElement;
}
