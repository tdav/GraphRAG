using GraphRag.Entities;
using GraphRag.Finalization;
using GraphRag.Relationships;

namespace ManagedCode.GraphRag.Tests.Integration.Finalization;

public class GraphFinalizerTests
{
    private static readonly EntitySeed[] SampleEntities =
    {
        new("Alice", "Person", "Researcher", new[] { "tu-1", "tu-2" }, 3),
        new("Bob", "Person", "Engineer", new[] { "tu-2" }, 2),
        new("Discovery", "Concept", "Important concept", Array.Empty<string>(), 1)
    };

    private static readonly RelationshipSeed[] SampleRelationships =
    {
        new("Alice", "Bob", "Collaborates with", 0.8, new[] { "tu-1" }),
        new("Alice", "Discovery", "Wrote about", 0.6, new[] { "tu-2" })
    };

    [Test]
    public async Task FinalizeGraph_AssignsZeroLayout_WhenLayoutDisabled()
    {
        var result = GraphFinalizer.Finalize(SampleEntities, SampleRelationships);

        await Assert.That(result.Entities.Count).IsEqualTo(SampleEntities.Length);
        await Assert.That(result.Relationships.Count).IsEqualTo(SampleRelationships.Length);

        foreach (var entity in result.Entities)
        {
            await Assert.That(entity.Id).IsNotNull();
            await Assert.That(entity.HumanReadableId >= 0).IsTrue();
            await Assert.That(entity.Degree).IsEqualTo(entity.Title == "Alice" ? 2 : entity.Title == "Bob" ? 1 : 1);
            await Assert.That(entity.X).IsEqualTo(0);
            await Assert.That(entity.Y).IsEqualTo(0);
        }

        foreach (var relationship in result.Relationships)
        {
            await Assert.That(relationship.Id).IsNotNull();
            await Assert.That(relationship.HumanReadableId >= 0).IsTrue();
            await Assert.That(relationship.CombinedDegree > 0).IsTrue();
        }

        await Assert.That(result.Entities.Sum(e => e.X)).IsEqualTo(0);
        await Assert.That(result.Entities.Sum(e => e.Y)).IsEqualTo(0);
    }

    [Test]
    public async Task FinalizeGraph_AssignsCircularLayout_WhenLayoutEnabled()
    {
        var options = new GraphFinalizerOptions(LayoutEnabled: true);
        var result = GraphFinalizer.Finalize(SampleEntities, SampleRelationships, options);

        await Assert.That(result.Entities).Contains(e => Math.Abs(e.X) > double.Epsilon || Math.Abs(e.Y) > double.Epsilon);
    }
}
