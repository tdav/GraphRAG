using GraphRag.Entities;
using GraphRag.Finalization;
using GraphRag.Relationships;

namespace ManagedCode.GraphRag.Tests.Finalization;

public sealed class GraphFinalizerTests
{
    [Test]
    public async Task Finalize_ComputesDegreesWithoutLayout()
    {
        var entities = new[]
        {
            new EntitySeed("Alice", "Person", "A", new[] { "unit-1" }, 2),
            new EntitySeed("Bob", "Person", null, new[] { "unit-2" }, 1)
        };

        var relationships = new[]
        {
            new RelationshipSeed("Alice", "Bob", "Friends", 0.5, new[] { "unit-1" })
        };

        var result = GraphFinalizer.Finalize(entities, relationships, new GraphFinalizerOptions(LayoutEnabled: false));

        await Assert.That(result.Entities.Count).IsEqualTo(2);
        var alice = result.Entities.Single(e => e.Title == "Alice");
        await Assert.That(alice.Degree).IsEqualTo(1);
        await Assert.That(alice.X).IsEqualTo(0d);
        await Assert.That(alice.Y).IsEqualTo(0d);

        await Assert.That(result.Relationships).HasSingleItem();
        var relationship = result.Relationships.Single();
        await Assert.That(relationship.CombinedDegree).IsEqualTo(2);
    }

    [Test]
    public async Task Finalize_ComputesLayoutWhenEnabled()
    {
        var entities = new[]
        {
            new EntitySeed("Alice", "Person", "A", new[] { "unit-1" }, 1),
            new EntitySeed("Bob", "Person", null, new[] { "unit-2" }, 1),
            new EntitySeed("Charlie", "Person", null, new[] { "unit-3" }, 1)
        };

        var result = GraphFinalizer.Finalize(entities, Array.Empty<RelationshipSeed>(), new GraphFinalizerOptions(LayoutEnabled: true));

        await Assert.That(result.Entities.Any(e => Math.Abs(e.X) > 0.001 || Math.Abs(e.Y) > 0.001)).IsTrue();
    }
}
