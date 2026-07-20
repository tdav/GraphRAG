using GraphRag.Vectors;

namespace ManagedCode.GraphRag.Tests.Vectors;

public sealed class VectorSearchResultTests
{
    [Test]
    public async Task Equals_ComparesMetadata()
    {
        var metadata = new Dictionary<string, object?> { ["chunk"] = 1, ["label"] = "alpha" };
        var first = new VectorSearchResult("id", 0.9, metadata);
        var second = new VectorSearchResult("id", 0.9, new Dictionary<string, object?>(metadata));

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(second.GetHashCode()).IsEqualTo(first.GetHashCode());
    }

    [Test]
    public async Task Equals_ReturnsFalseForDifferentIds()
    {
        var first = new VectorSearchResult("id-a", 0.9);
        var second = new VectorSearchResult("id-b", 0.9);

        await Assert.That(second).IsNotEqualTo(first);
        await Assert.That(second.GetHashCode()).IsNotEqualTo(first.GetHashCode());
    }
}
