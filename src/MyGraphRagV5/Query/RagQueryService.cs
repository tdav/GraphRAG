using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using GraphRag.Vectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MyGraphRagV5.Ai;
using MyGraphRagV5.Data;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace MyGraphRagV5.Query;

/// <summary>
/// Answers questions over a project's indexed knowledge: embeds the question, retrieves
/// vector-store passages, expands entity hits one hop through the graph, reranks the combined
/// candidates, prompts the chat model with the top passages, and persists the exchange to
/// chat history.
/// </summary>
public sealed class RagQueryService(
    IServiceScopeFactory scopeFactory,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IVectorStore vectorStore,
    IProjectGraphStoreProvider graphStoreProvider,
    IReranker reranker,
    IChatClient chatClient)
{
    private const int VectorSearchLimit = 20;
    private const int MaxExpandedEntities = 10;
    private const int MaxGraphFacts = 30;
    private const int RerankTopN = 8;
    private const int SnippetLength = 200;
    private const int TitleMaxLength = 60;

    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
    private readonly IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = embeddingGenerator;
    private readonly IVectorStore vectorStore = vectorStore;
    private readonly IProjectGraphStoreProvider graphStoreProvider = graphStoreProvider;
    private readonly IReranker reranker = reranker;
    private readonly IChatClient chatClient = chatClient;

    public async Task<RagAnswer> AskAsync(
        Guid projectId,
        string question,
        Guid? chatSessionId = null,
        CancellationToken ct = default)
    {
        var passages = await this.RetrievePassagesAsync(projectId, question, ct).ConfigureAwait(false);
        var messages = BuildPrompt(question, passages);

        var response = await this.chatClient.GetResponseAsync(messages, cancellationToken: ct).ConfigureAwait(false);
        var answer = response.Text;
        var sources = BuildSources(passages);

        await this.PersistExchangeAsync(projectId, chatSessionId, question, answer, sources, ct).ConfigureAwait(false);

        return new RagAnswer(answer, sources);
    }

    public async IAsyncEnumerable<string> StreamAnswerAsync(
        Guid projectId,
        string question,
        Guid? chatSessionId = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var passages = await this.RetrievePassagesAsync(projectId, question, ct).ConfigureAwait(false);
        var messages = BuildPrompt(question, passages);

        var accumulated = new StringBuilder();
        await foreach (var update in this.chatClient.GetStreamingResponseAsync(messages, cancellationToken: ct).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(update.Text))
            {
                continue;
            }

            accumulated.Append(update.Text);
            yield return update.Text;
        }

        var sources = BuildSources(passages);
        await this.PersistExchangeAsync(projectId, chatSessionId, question, accumulated.ToString(), sources, ct).ConfigureAwait(false);
    }

    /// <summary>Shared retrieval pipeline: vector search → graph expansion → rerank.</summary>
    private async Task<IReadOnlyList<RankedPassage>> RetrievePassagesAsync(Guid projectId, string question, CancellationToken ct)
    {
        RagProject project;
        using (var scope = this.scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            project = await db.RagProjects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId, ct)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Project '{projectId}' was not found.");
        }

        var embeddings = await this.embeddingGenerator
            .GenerateAsync([question], cancellationToken: ct)
            .ConfigureAwait(false);
        var queryVector = embeddings[0].Vector;

        var searchResults = new List<VectorSearchResult>();
        await foreach (var result in this.vectorStore.SearchAsync(project.VectorCollection, queryVector, VectorSearchLimit, ct).ConfigureAwait(false))
        {
            searchResults.Add(result);
        }

        var candidates = BuildVectorCandidates(searchResults);
        candidates.AddRange(await this.ExpandGraphFactsAsync(project.GraphName, searchResults, ct).ConfigureAwait(false));

        return await this.RerankAsync(question, candidates, ct).ConfigureAwait(false);
    }

    private static List<CandidatePassage> BuildVectorCandidates(IReadOnlyList<VectorSearchResult> results)
    {
        var candidates = new List<CandidatePassage>(results.Count);
        foreach (var result in results)
        {
            var text = MetadataString(result.Metadata, "text");
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var title = MetadataString(result.Metadata, "title") ?? result.Id;
            candidates.Add(new CandidatePassage(result.Id, title, text));
        }

        return candidates;
    }

    private async Task<List<CandidatePassage>> ExpandGraphFactsAsync(
        string graphName,
        IReadOnlyList<VectorSearchResult> searchResults,
        CancellationToken ct)
    {
        var entityTitles = searchResults
            .Where(r => MetadataString(r.Metadata, "type") == "entity")
            .Select(r => MetadataString(r.Metadata, "title"))
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxExpandedEntities)
            .ToList();

        var facts = new List<CandidatePassage>();
        if (entityTitles.Count == 0)
        {
            return facts;
        }

        var store = await this.graphStoreProvider.GetStoreAsync(graphName, ct).ConfigureAwait(false);

        foreach (var title in entityTitles)
        {
            if (facts.Count >= MaxGraphFacts)
            {
                break;
            }

            await foreach (var relationship in store.GetOutgoingRelationshipsAsync(title!, ct).ConfigureAwait(false))
            {
                if (facts.Count >= MaxGraphFacts)
                {
                    break;
                }

                var description = relationship.Properties.TryGetValue("description", out var value) ? value?.ToString() : null;
                var fact = string.IsNullOrWhiteSpace(description)
                    ? $"{relationship.SourceId} —{relationship.Type}→ {relationship.TargetId}"
                    : $"{relationship.SourceId} —{relationship.Type}→ {relationship.TargetId}: {description}";

                facts.Add(new CandidatePassage(
                    $"relationship:{relationship.SourceId}->{relationship.TargetId}:{relationship.Type}",
                    relationship.SourceId,
                    fact));
            }
        }

        return facts;
    }

    private async Task<IReadOnlyList<RankedPassage>> RerankAsync(string question, List<CandidatePassage> candidates, CancellationToken ct)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var texts = candidates.Select(c => c.Text).ToList();
        var rerankResults = await this.reranker.RerankAsync(question, texts, RerankTopN, ct).ConfigureAwait(false);

        var ranked = new List<RankedPassage>(rerankResults.Count);
        foreach (var r in rerankResults)
        {
            var c = candidates[r.Index];
            ranked.Add(new RankedPassage(c.Id, c.Title, c.Text, r.Score));
        }

        return ranked;
    }

    private static IReadOnlyList<AiChatMessage> BuildPrompt(string question, IReadOnlyList<RankedPassage> passages)
    {
        const string systemPrompt =
            "Answer the question using ONLY the numbered context passages below. " +
            "Cite the passages you rely on inline as [n], matching the passage numbers. " +
            "If the context does not contain the answer, say so instead of guessing.";

        var context = string.Join(
            "\n\n",
            passages.Select((p, i) => $"[{i + 1}] {p.Text}"));

        var userPrompt = $"Context:\n{context}\n\nQuestion: {question}";

        return
        [
            new AiChatMessage(ChatRole.System, systemPrompt),
            new AiChatMessage(ChatRole.User, userPrompt),
        ];
    }

    private static IReadOnlyList<SourceRef> BuildSources(IReadOnlyList<RankedPassage> passages) =>
        passages
            .Select(p => new SourceRef(p.Id, p.Title, Snippet(p.Text), p.Score))
            .ToList();

    private static string Snippet(string text) => text.Length <= SnippetLength ? text : text[..SnippetLength];

    private async Task PersistExchangeAsync(
        Guid projectId,
        Guid? chatSessionId,
        string question,
        string answer,
        IReadOnlyList<SourceRef> sources,
        CancellationToken ct)
    {
        using var scope = this.scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sessionId = chatSessionId;
        if (sessionId is null)
        {
            var title = question.Length <= TitleMaxLength ? question : question[..TitleMaxLength];
            var session = new ChatSession
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = title,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.ChatSessions.Add(session);
            sessionId = session.Id;
        }

        db.ChatMessages.Add(new MyGraphRagV5.Data.ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId.Value,
            Role = "user",
            Content = question,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.ChatMessages.Add(new MyGraphRagV5.Data.ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId.Value,
            Role = "assistant",
            Content = answer,
            SourcesJson = JsonSerializer.Serialize(sources),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        // Persistence must complete even if the caller's token was cancelled mid-stream after the
        // answer was already fully produced; losing a finished exchange would be worse than a late write.
        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static string? MetadataString(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out var value) ? value as string ?? value?.ToString() : null;

    private sealed record CandidatePassage(string Id, string Title, string Text);

    private sealed record RankedPassage(string Id, string Title, string Text, double Score);
}

public sealed record RagAnswer(string Answer, IReadOnlyList<SourceRef> Sources);

public sealed record SourceRef(string Id, string Title, string Snippet, double Score);
