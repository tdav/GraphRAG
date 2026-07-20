using System.Collections.Immutable;
using GraphRag.Community;
using GraphRag.Data;
using GraphRag.Entities;
using GraphRag.Relationships;
using MyGraphRagV5.Indexing;

namespace MyGraphRagV5.Tests.Indexing;

public class IndexingMappingTests
{
    private static EntityRecord Entity(string title = "Ada Lovelace", string? description = "First programmer") =>
        new(
            Id: "e1",
            HumanReadableId: 7,
            Title: title,
            Type: "PERSON",
            Description: description,
            TextUnitIds: ImmutableArray<string>.Empty,
            Frequency: 3,
            Degree: 5,
            X: 0,
            Y: 0);

    [Test]
    public async Task ToNode_UsesTitleAsId_AndEntityLabel()
    {
        var node = IndexingService.ToNode(Entity());

        await Assert.That(node.Id).IsEqualTo("Ada Lovelace");
        await Assert.That(node.Label).IsEqualTo("Entity");
        await Assert.That(node.Properties["type"]).IsEqualTo("PERSON");
        await Assert.That(node.Properties["description"]).IsEqualTo("First programmer");
        await Assert.That(node.Properties["entityId"]).IsEqualTo("e1");
        await Assert.That((int)node.Properties["frequency"]!).IsEqualTo(3);
        await Assert.That((int)node.Properties["degree"]!).IsEqualTo(5);
    }

    [Test]
    public async Task ToRelationship_MapsEndpointsTypeAndBidirectional()
    {
        var record = new RelationshipRecord(
            "r1", 2, "Ada Lovelace", "Analytical Engine", "worked_on",
            "designed programs for", 1.5, 9, ImmutableArray<string>.Empty, Bidirectional: true);

        var rel = IndexingService.ToRelationship(record);

        await Assert.That(rel.SourceId).IsEqualTo("Ada Lovelace");
        await Assert.That(rel.TargetId).IsEqualTo("Analytical Engine");
        await Assert.That(rel.Type).IsEqualTo("worked_on");
        await Assert.That(rel.Bidirectional).IsTrue();
        await Assert.That(rel.Properties["description"]).IsEqualTo("designed programs for");
        await Assert.That((double)rel.Properties["weight"]!).IsEqualTo(1.5);
        await Assert.That(rel.Properties["relationshipId"]).IsEqualTo("r1");
    }

    [Test]
    [Arguments("works for", "works_for")]
    [Arguments("", "RELATED_TO")]
    [Arguments("   ", "RELATED_TO")]
    [Arguments("3-way", "_3_way")]
    [Arguments("RELATED_TO", "RELATED_TO")]
    public async Task NormalizeRelationshipType_ProducesValidAgeLabel(string input, string expected)
    {
        await Assert.That(IndexingService.NormalizeRelationshipType(input)).IsEqualTo(expected);
    }

    [Test]
    public async Task TextUnitEmbedding_CarriesIdTextTypeTitle()
    {
        var unit = new TextUnitRecord { Id = "tu1", Text = "hello world" };

        var item = IndexingService.TextUnitEmbedding(unit);

        await Assert.That(item.Text).IsEqualTo("hello world");
        await Assert.That(item.Metadata["id"]).IsEqualTo("text_unit:tu1");
        await Assert.That(item.Metadata["text"]).IsEqualTo("hello world");
        await Assert.That(item.Metadata["type"]).IsEqualTo("text_unit");
        await Assert.That(item.Metadata["title"]).IsEqualTo("tu1");
    }

    [Test]
    public async Task CommunityEmbedding_EmbedsSummary()
    {
        var report = new CommunityReportRecord("c1", 1, new[] { "Ada" }, "Community summary text", new[] { "kw" });

        var item = IndexingService.CommunityEmbedding(report);

        await Assert.That(item.Text).IsEqualTo("Community summary text");
        await Assert.That(item.Metadata["id"]).IsEqualTo("community:c1");
        await Assert.That(item.Metadata["type"]).IsEqualTo("community");
    }

    [Test]
    public async Task EntityEmbedding_CombinesNameAndDescription()
    {
        var item = IndexingService.EntityEmbedding(Entity("Ada", "the first programmer"));

        await Assert.That(item.Text).IsEqualTo("Ada. the first programmer");
        await Assert.That(item.Metadata["id"]).IsEqualTo("entity:Ada");
        await Assert.That(item.Metadata["type"]).IsEqualTo("entity");
        await Assert.That(item.Metadata["title"]).IsEqualTo("Ada");
    }

    [Test]
    public async Task EntityEmbedding_FallsBackToTitleWhenNoDescription()
    {
        var item = IndexingService.EntityEmbedding(Entity("Ada", description: null));

        await Assert.That(item.Text).IsEqualTo("Ada");
    }
}
