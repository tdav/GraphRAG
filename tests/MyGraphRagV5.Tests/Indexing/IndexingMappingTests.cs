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

    [Fact]
    public void ToNode_UsesTitleAsId_AndEntityLabel()
    {
        var node = IndexingService.ToNode(Entity());

        Assert.Equal("Ada Lovelace", node.Id);
        Assert.Equal("Entity", node.Label);
        Assert.Equal("PERSON", node.Properties["type"]);
        Assert.Equal("First programmer", node.Properties["description"]);
        Assert.Equal("e1", node.Properties["entityId"]);
        Assert.Equal(3, (int)node.Properties["frequency"]!);
        Assert.Equal(5, (int)node.Properties["degree"]!);
    }

    [Fact]
    public void ToRelationship_MapsEndpointsTypeAndBidirectional()
    {
        var record = new RelationshipRecord(
            "r1", 2, "Ada Lovelace", "Analytical Engine", "worked_on",
            "designed programs for", 1.5, 9, ImmutableArray<string>.Empty, Bidirectional: true);

        var rel = IndexingService.ToRelationship(record);

        Assert.Equal("Ada Lovelace", rel.SourceId);
        Assert.Equal("Analytical Engine", rel.TargetId);
        Assert.Equal("worked_on", rel.Type);
        Assert.True(rel.Bidirectional);
        Assert.Equal("designed programs for", rel.Properties["description"]);
        Assert.Equal(1.5, (double)rel.Properties["weight"]!);
        Assert.Equal("r1", rel.Properties["relationshipId"]);
    }

    [Theory]
    [InlineData("works for", "works_for")]
    [InlineData("", "RELATED_TO")]
    [InlineData("   ", "RELATED_TO")]
    [InlineData("3-way", "_3_way")]
    [InlineData("RELATED_TO", "RELATED_TO")]
    public void NormalizeRelationshipType_ProducesValidAgeLabel(string input, string expected)
    {
        Assert.Equal(expected, IndexingService.NormalizeRelationshipType(input));
    }

    [Fact]
    public void TextUnitEmbedding_CarriesIdTextTypeTitle()
    {
        var unit = new TextUnitRecord { Id = "tu1", Text = "hello world" };

        var item = IndexingService.TextUnitEmbedding(unit);

        Assert.Equal("hello world", item.Text);
        Assert.Equal("text_unit:tu1", item.Metadata["id"]);
        Assert.Equal("hello world", item.Metadata["text"]);
        Assert.Equal("text_unit", item.Metadata["type"]);
        Assert.Equal("tu1", item.Metadata["title"]);
    }

    [Fact]
    public void CommunityEmbedding_EmbedsSummary()
    {
        var report = new CommunityReportRecord("c1", 1, new[] { "Ada" }, "Community summary text", new[] { "kw" });

        var item = IndexingService.CommunityEmbedding(report);

        Assert.Equal("Community summary text", item.Text);
        Assert.Equal("community:c1", item.Metadata["id"]);
        Assert.Equal("community", item.Metadata["type"]);
    }

    [Fact]
    public void EntityEmbedding_CombinesNameAndDescription()
    {
        var item = IndexingService.EntityEmbedding(Entity("Ada", "the first programmer"));

        Assert.Equal("Ada. the first programmer", item.Text);
        Assert.Equal("entity:Ada", item.Metadata["id"]);
        Assert.Equal("entity", item.Metadata["type"]);
        Assert.Equal("Ada", item.Metadata["title"]);
    }

    [Fact]
    public void EntityEmbedding_FallsBackToTitleWhenNoDescription()
    {
        var item = IndexingService.EntityEmbedding(Entity("Ada", description: null));

        Assert.Equal("Ada", item.Text);
    }
}
