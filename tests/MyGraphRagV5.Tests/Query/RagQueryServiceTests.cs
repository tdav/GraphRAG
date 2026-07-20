using GraphRag.Graphs;
using GraphRag.Vectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MyGraphRagV5.Ai;
using MyGraphRagV5.Data;
using MyGraphRagV5.Query;

namespace MyGraphRagV5.Tests.Query;

/// <summary>
/// Orchestration tests for <see cref="RagQueryService"/> using fake AI/store dependencies plus an
/// EF Core InMemory-backed <see cref="AppDbContext"/> (project lookup and chat-history persistence
/// are unconditional parts of the flow, so a working DbContext is required to exercise it at all).
/// A real Postgres/AGE + pgvector round trip is out of scope here; see Task 10.
/// </summary>
public class RagQueryServiceTests
{
    private static (IServiceScopeFactory ScopeFactory, Guid ProjectId) CreateSeededScopeFactory()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        var projectId = Guid.NewGuid();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RagProjects.Add(new RagProject
            {
                Id = projectId,
                Name = "test-project",
                SourceFolder = "C:/src",
                GraphName = "graph_test",
                VectorCollection = "vec_test",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            db.SaveChanges();
        }

        return (provider.GetRequiredService<IServiceScopeFactory>(), projectId);
    }

    private static VectorSearchResult Result(string id, double score, string type, string title, string text) =>
        new(id, score, new Dictionary<string, object?>
        {
            ["id"] = id,
            ["text"] = text,
            ["type"] = type,
            ["title"] = title,
        });

    [Fact]
    public async Task AskAsync_RunsRetrievalGraphExpansionRerankPrompt_AndReturnsAnswerWithSources()
    {
        var (scopeFactory, projectId) = CreateSeededScopeFactory();

        var vectorStore = new FakeVectorStore();
        vectorStore.Results.AddRange(
        [
            Result("text_unit:tu1", 0.4, "text_unit", "tu1", "Ada Lovelace wrote the first computer algorithm for the Analytical Engine."),
            Result("entity:Ada", 0.9, "entity", "Ada", "Ada. The first programmer."),
            Result("community:c1", 0.3, "community", "Community 1", string.Empty), // empty text must be excluded
        ]);

        var graphStore = new FakeGraphStore();
        graphStore.OutgoingBySource["Ada"] =
        [
            new GraphRelationship("Ada", "Analytical Engine", "invented", new Dictionary<string, object?>
            {
                ["description"] = "Ada designed programs for it.",
            }),
        ];
        var graphProvider = new FakeProjectGraphStoreProvider(graphStore);

        // Known ordering: graph fact ranked first, then the text unit, then the entity blurb.
        var reranker = new FakeReranker(
        [
            new RerankResult(2, 0.95),
            new RerankResult(0, 0.80),
            new RerankResult(1, 0.50),
        ]);

        var chatClient = new FakeChatClient("Ada invented computing concepts [1][2].");

        var service = new RagQueryService(
            scopeFactory,
            new FakeEmbeddingGenerator(),
            vectorStore,
            graphProvider,
            reranker,
            chatClient);

        var answer = await service.AskAsync(projectId, "Who is Ada?", ct: CancellationToken.None);

        // Answer comes straight from the chat client.
        Assert.Equal("Ada invented computing concepts [1][2].", answer.Answer);

        // Vector search hit the project's own collection.
        Assert.Equal("vec_test", vectorStore.LastCollection);
        Assert.Equal(20, vectorStore.LastLimit);

        // Graph expansion used the project's graph and the entity-type hit's title.
        Assert.Equal("graph_test", graphProvider.LastGraphName);
        Assert.Contains("Ada", graphStore.RequestedSourceIds);

        // The relationship fact reached the rerank input alongside the two text passages
        // (the empty-text community candidate was excluded).
        Assert.Equal(3, reranker.LastTexts!.Count);
        Assert.Contains(reranker.LastTexts!, t => t.Contains("Ada —invented→ Analytical Engine"));
        Assert.Equal(8, reranker.LastTopN);

        // Sources are assembled from the reranked candidates, in rerank order.
        Assert.Equal(3, answer.Sources.Count);

        Assert.Equal("relationship:Ada->Analytical Engine:invented", answer.Sources[0].Id);
        Assert.Equal("Ada", answer.Sources[0].Title);
        Assert.Equal("Ada —invented→ Analytical Engine: Ada designed programs for it.", answer.Sources[0].Snippet);
        Assert.Equal(0.95, answer.Sources[0].Score);

        Assert.Equal("text_unit:tu1", answer.Sources[1].Id);
        Assert.Equal("tu1", answer.Sources[1].Title);
        Assert.Equal(0.80, answer.Sources[1].Score);

        Assert.Equal("entity:Ada", answer.Sources[2].Id);
        Assert.Equal("Ada", answer.Sources[2].Title);
        Assert.Equal(0.50, answer.Sources[2].Score);

        // Prompt numbers the passages in rerank order and instructs context-only, cited answers.
        var systemMessage = Assert.Single(chatClient.LastMessages!, m => m.Role == ChatRole.System);
        Assert.Contains("ONLY", systemMessage.Text);
        var userMessage = Assert.Single(chatClient.LastMessages!, m => m.Role == ChatRole.User);
        Assert.Contains("[1] Ada —invented→ Analytical Engine", userMessage.Text);
        Assert.Contains("Who is Ada?", userMessage.Text);

        // Persisted to chat history: a new session plus the user/assistant exchange.
        using var verifyScope = scopeFactory.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = Assert.Single(db.ChatSessions.Where(s => s.ProjectId == projectId));
        var messages = db.ChatMessages.Where(m => m.SessionId == session.Id).ToList();
        Assert.Equal(2, messages.Count);
        var userRow = Assert.Single(messages, m => m.Role == "user");
        Assert.Equal("Who is Ada?", userRow.Content);
        var assistantRow = Assert.Single(messages, m => m.Role == "assistant");
        Assert.Equal(answer.Answer, assistantRow.Content);
        Assert.NotNull(assistantRow.SourcesJson);
        // System.Text.Json escapes '>' by default; check for the unambiguous, unescaped prefix.
        Assert.Contains("relationship:Ada-", assistantRow.SourcesJson);
    }

    [Fact]
    public async Task StreamAnswerAsync_YieldsChunksInOrder_AndPersistsAccumulatedAnswer()
    {
        var (scopeFactory, projectId) = CreateSeededScopeFactory();

        var vectorStore = new FakeVectorStore();
        vectorStore.Results.Add(Result("text_unit:tu1", 0.5, "text_unit", "tu1", "Some passage text."));

        var graphProvider = new FakeProjectGraphStoreProvider(new FakeGraphStore());
        var reranker = new FakeReranker([new RerankResult(0, 0.7)]);
        var chatClient = new FakeChatClient("Full answer.", streamChunks: ["Full ", "answer."]);

        var service = new RagQueryService(
            scopeFactory,
            new FakeEmbeddingGenerator(),
            vectorStore,
            graphProvider,
            reranker,
            chatClient);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamAnswerAsync(projectId, "Q?", ct: CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["Full ", "answer."], chunks);

        using var verifyScope = scopeFactory.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assistantRow = db.ChatMessages.Single(m => m.Role == "assistant");
        Assert.Equal("Full answer.", assistantRow.Content);
    }

    [Fact]
    public async Task AskAsync_ReusesProvidedChatSessionId_InsteadOfCreatingANewOne()
    {
        var (scopeFactory, projectId) = CreateSeededScopeFactory();
        Guid sessionId;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = new ChatSession { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Existing", CreatedAt = DateTimeOffset.UtcNow };
            db.ChatSessions.Add(session);
            db.SaveChanges();
            sessionId = session.Id;
        }

        var vectorStore = new FakeVectorStore();
        vectorStore.Results.Add(Result("text_unit:tu1", 0.5, "text_unit", "tu1", "Text."));
        var service = new RagQueryService(
            scopeFactory,
            new FakeEmbeddingGenerator(),
            vectorStore,
            new FakeProjectGraphStoreProvider(new FakeGraphStore()),
            new FakeReranker([new RerankResult(0, 0.5)]),
            new FakeChatClient("Answer."));

        await service.AskAsync(projectId, "Q?", chatSessionId: sessionId, ct: CancellationToken.None);

        using var verifyScope = scopeFactory.CreateScope();
        var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, db2.ChatSessions.Count(s => s.ProjectId == projectId));
        Assert.Equal(2, db2.ChatMessages.Count(m => m.SessionId == sessionId));
    }

    [Fact]
    public async Task AskAsync_ThrowsWhenProjectNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        var service = new RagQueryService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeEmbeddingGenerator(),
            new FakeVectorStore(),
            new FakeProjectGraphStoreProvider(new FakeGraphStore()),
            new FakeReranker([]),
            new FakeChatClient("unused"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AskAsync(Guid.NewGuid(), "Q?", ct: CancellationToken.None));
    }
}
