namespace MyGraphRagV5.Tests.Integration;

[CollectionDefinition(Name)]
public sealed class AgePgVectorCollection : ICollectionFixture<AgePgVectorFixture>
{
    public const string Name = "AgePgVector integration";
}
