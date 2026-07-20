using System.Collections.Immutable;
using GraphRag;
using GraphRag.Callbacks;
using GraphRag.Community;
using GraphRag.Config;
using GraphRag.Constants;
using GraphRag.Entities;
using GraphRag.Indexing.Runtime;
using GraphRag.Indexing.Workflows;
using GraphRag.Relationships;
using GraphRag.Storage;
using ManagedCode.GraphRag.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.GraphRag.Tests.Workflows;

public sealed class CreateCommunitiesWorkflowTests
{
    [Test]
    public async Task RunWorkflow_GroupsEntitiesAndPersistsCommunities()
    {
        var outputStorage = new MemoryPipelineStorage();
        await outputStorage.WriteTableAsync(PipelineTableNames.Entities, new[]
        {
            new EntityRecord("entity-alice", 0, "Alice", "person", "Researcher", new[] { "unit-1" }.ToImmutableArray(), 3, 2, 0, 0),
            new EntityRecord("entity-bob", 1, "Bob", "person", "Policy expert", new[] { "unit-2" }.ToImmutableArray(), 2, 2, 0, 0),
            new EntityRecord("entity-carol", 2, "Carol", "person", "Analyst", new[] { "unit-3" }.ToImmutableArray(), 1, 1, 0, 0),
            new EntityRecord("entity-dave", 3, "Dave", "person", "Observer", new[] { "unit-4" }.ToImmutableArray(), 1, 0, 0, 0)
        });

        await outputStorage.WriteTableAsync(PipelineTableNames.Relationships, new[]
        {
            new RelationshipRecord("rel-1", 0, "Alice", "Bob", "collaborates_with", null, 0.8, 3, new[] { "unit-1", "unit-2" }.ToImmutableArray(), true),
            new RelationshipRecord("rel-2", 1, "Bob", "Carol", "mentors", null, 0.6, 3, new[] { "unit-3" }.ToImmutableArray(), false)
        });

        var context = new PipelineRunContext(
            inputStorage: new MemoryPipelineStorage(),
            outputStorage: outputStorage,
            previousStorage: new MemoryPipelineStorage(),
            cache: new StubPipelineCache(),
            callbacks: NoopWorkflowCallbacks.Instance,
            stats: new PipelineRunStats(),
            state: new PipelineState(),
            services: new ServiceCollection().AddGraphRag().BuildServiceProvider());

        var workflow = CreateCommunitiesWorkflow.Create();
        var config = new GraphRagConfig
        {
            ClusterGraph = new ClusterGraphConfig
            {
                MaxClusterSize = 2,
                UseLargestConnectedComponent = false,
                Seed = 1337
            }
        };

        await workflow(config, context, CancellationToken.None);

        var communities = await outputStorage.LoadTableAsync<CommunityRecord>(PipelineTableNames.Communities);
        await Assert.That(communities.Count).IsEqualTo(3);
        await Assert.That(context.Items.TryGetValue("create_communities:count", out var countValue)).IsTrue();
        await Assert.That(countValue).IsTypeOf<int>();
        await Assert.That((int)countValue!).IsEqualTo(3);

        var communityByMembers = communities.ToDictionary(
            community => community.EntityIds.OrderBy(id => id).ToArray(),
            community => community,
            new SequenceComparer<string>());

        var aliceBob = communityByMembers[new[] { "entity-alice", "entity-bob" }];
        await Assert.That(aliceBob.Size).IsEqualTo(2);
        await Assert.That(aliceBob.HumanReadableId).IsEqualTo(aliceBob.CommunityId);
        await Assert.That(aliceBob.RelationshipIds).Contains("rel-1");
        await Assert.That(aliceBob.TextUnitIds).Contains("unit-1");
        await Assert.That(aliceBob.TextUnitIds).Contains("unit-2");
        await Assert.That(aliceBob.ParentId).IsEqualTo(-1);

        var carol = communityByMembers[new[] { "entity-carol" }];
        await Assert.That(carol.RelationshipIds).IsEmpty();
        await Assert.That(carol.TextUnitIds).Contains("unit-3");

        var dave = communityByMembers[new[] { "entity-dave" }];
        await Assert.That(dave.RelationshipIds).IsEmpty();
        await Assert.That(dave.TextUnitIds).Contains("unit-4");
    }

    private sealed class SequenceComparer<T> : IEqualityComparer<IReadOnlyList<T>> where T : notnull
    {
        public bool Equals(IReadOnlyList<T>? x, IReadOnlyList<T>? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            if (x.Count != y.Count)
            {
                return false;
            }

            for (var index = 0; index < x.Count; index++)
            {
                if (!EqualityComparer<T>.Default.Equals(x[index], y[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(IReadOnlyList<T> obj)
        {
            var hash = new HashCode();
            foreach (var item in obj)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }
    }
}
