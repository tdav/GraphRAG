using System.Collections.Immutable;
using GraphRag.Relationships;

namespace ManagedCode.GraphRag.Tests.Relationships;

public sealed class RelationshipRecordTests
{
    [Test]
    public async Task RelationshipRecord_StoresValues()
    {
        var textUnits = ImmutableArray.Create("unit-1", "unit-2");
        var record = new RelationshipRecord("rel-1", 7, "source", "target", "related_to", "description", 0.42, 3, textUnits, true);

        await Assert.That(record.Id).IsEqualTo("rel-1");
        await Assert.That(record.HumanReadableId).IsEqualTo(7);
        await Assert.That(record.Source).IsEqualTo("source");
        await Assert.That(record.Target).IsEqualTo("target");
        await Assert.That(record.Type).IsEqualTo("related_to");
        await Assert.That(record.Description).IsEqualTo("description");
        await Assert.That(record.Weight).IsEqualTo(0.42);
        await Assert.That(record.CombinedDegree).IsEqualTo(3);
        await Assert.That(record.TextUnitIds).IsEquivalentTo(textUnits);
        await Assert.That(record.Bidirectional).IsTrue();
    }

    [Test]
    public async Task RelationshipSeed_StoresValues()
    {
        var textUnits = new List<string> { "unit-1", "unit-2" };
        var seed = new RelationshipSeed("source", "target", "seed", 0.33, textUnits)
        {
            Type = "influences",
            Bidirectional = true
        };

        await Assert.That(seed.Source).IsEqualTo("source");
        await Assert.That(seed.Target).IsEqualTo("target");
        await Assert.That(seed.Description).IsEqualTo("seed");
        await Assert.That(seed.Weight).IsEqualTo(0.33);
        await Assert.That(seed.TextUnitIds).IsEquivalentTo(textUnits);
        await Assert.That(seed.Type).IsEqualTo("influences");
        await Assert.That(seed.Bidirectional).IsTrue();
    }
}
