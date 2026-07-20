using System.Text.Json;
using MyGraphRagV5.Vectors;

namespace MyGraphRagV5.Tests.Vectors;

public class PgVectorNamingTests
{
    [Theory]
    [InlineData("MyCollection", "mycollection")]
    [InlineData("my collection", "my_collection")]
    [InlineData("my-collection!", "my_collection_")]
    [InlineData("already_ok_123", "already_ok_123")]
    [InlineData("Über Café", "_ber_caf_")]
    public void SanitizeCollectionName_ProducesLowercaseAsciiIdentifier(string input, string expected)
    {
        var sanitized = PgVectorNaming.SanitizeCollectionName(input);

        Assert.Equal(expected, sanitized);
        Assert.Matches("^[a-z0-9_]+$", sanitized);
    }

    [Fact]
    public void SanitizeCollectionName_IsStableAcrossCalls()
    {
        var first = PgVectorNaming.SanitizeCollectionName("Some Collection");
        var second = PgVectorNaming.SanitizeCollectionName("Some Collection");

        Assert.Equal(first, second);
    }

    [Fact]
    public void SanitizeCollectionName_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => PgVectorNaming.SanitizeCollectionName(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeCollectionName_RejectsEmptyOrWhitespace(string input)
    {
        Assert.Throws<ArgumentException>(() => PgVectorNaming.SanitizeCollectionName(input));
    }

    [Fact]
    public void TableName_PrefixesSanitizedNameWithVec()
    {
        var table = PgVectorNaming.TableName("My Docs");

        Assert.Equal("vec_my_docs", table);
    }

    [Fact]
    public void ResolveId_UsesMetadataIdWhenPresent()
    {
        var metadata = new Dictionary<string, object?> { ["id"] = "chunk-42" };

        Assert.Equal("chunk-42", PgVectorNaming.ResolveId(metadata));
    }

    [Fact]
    public void ResolveId_StringifiesNonStringIdValues()
    {
        var metadata = new Dictionary<string, object?> { ["id"] = 42 };

        Assert.Equal("42", PgVectorNaming.ResolveId(metadata));
    }

    [Fact]
    public void ResolveId_GeneratesGuidWhenIdMissing()
    {
        var metadata = new Dictionary<string, object?>();

        var id = PgVectorNaming.ResolveId(metadata);

        Assert.True(Guid.TryParse(id, out _));
    }

    [Fact]
    public void ResolveId_GeneratesGuidWhenIdIsNull()
    {
        var metadata = new Dictionary<string, object?> { ["id"] = null };

        var id = PgVectorNaming.ResolveId(metadata);

        Assert.True(Guid.TryParse(id, out _));
    }

    [Fact]
    public void ResolveText_ReturnsTextWhenPresent()
    {
        var metadata = new Dictionary<string, object?> { ["text"] = "chunk body" };

        Assert.Equal("chunk body", PgVectorNaming.ResolveText(metadata));
    }

    [Fact]
    public void ResolveText_ReturnsNullWhenMissing()
    {
        var metadata = new Dictionary<string, object?>();

        Assert.Null(PgVectorNaming.ResolveText(metadata));
    }

    [Fact]
    public void JsonElementToClr_ConvertsStringToString()
    {
        var element = ParseElement("\"hello\"");

        Assert.Equal("hello", PgVectorNaming.JsonElementToClr(element));
    }

    [Fact]
    public void JsonElementToClr_ConvertsIntegralNumberToLong()
    {
        var element = ParseElement("42");

        var result = PgVectorNaming.JsonElementToClr(element);

        Assert.IsType<long>(result);
        Assert.Equal(42L, result);
    }

    [Fact]
    public void JsonElementToClr_ConvertsFractionalNumberToDouble()
    {
        var element = ParseElement("3.14");

        var result = PgVectorNaming.JsonElementToClr(element);

        Assert.IsType<double>(result);
        Assert.Equal(3.14, result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void JsonElementToClr_ConvertsBooleans(string json, bool expected)
    {
        var element = ParseElement(json);

        Assert.Equal(expected, PgVectorNaming.JsonElementToClr(element));
    }

    [Fact]
    public void JsonElementToClr_ConvertsNullToNull()
    {
        var element = ParseElement("null");

        Assert.Null(PgVectorNaming.JsonElementToClr(element));
    }

    [Fact]
    public void JsonElementToClr_ConvertsNestedObjectToDictionary()
    {
        var element = ParseElement("""{"inner": "value", "count": 2}""");

        var result = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(PgVectorNaming.JsonElementToClr(element));

        Assert.Equal("value", result["inner"]);
        Assert.Equal(2L, result["count"]);
    }

    [Fact]
    public void JsonElementToClr_ConvertsArrayToListOfConvertedElements()
    {
        var element = ParseElement("[1, \"two\", true, null]");

        var result = Assert.IsAssignableFrom<List<object?>>(PgVectorNaming.JsonElementToClr(element));

        Assert.Equal([1L, "two", true, null], result);
    }

    private static JsonElement ParseElement(string json) => JsonDocument.Parse(json).RootElement;
}
