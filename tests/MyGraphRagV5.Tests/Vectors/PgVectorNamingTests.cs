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
}
