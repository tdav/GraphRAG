using System.Net;
using System.Net.Http.Json;
using MyGraphRagV5.Ai;

namespace MyGraphRagV5.Tests.Ai;

public class TeiRerankerTests
{
    [Test]
    public async Task RerankAsync_PostsQueryAndTextsToRerankEndpoint()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new object[]
                {
                    new { index = 0, score = 0.1 },
                    new { index = 1, score = 0.9 },
                }),
            }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://tei-rerank.test") };
        var reranker = new TeiReranker(client);

        await reranker.RerankAsync("query", ["doc a", "doc b"], topN: 2, CancellationToken.None);

        await Assert.That(handler.Requests).HasSingleItem();
        var request = handler.Requests.Single();
        await Assert.That(request.Request.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(request.Request.RequestUri!.AbsolutePath).IsEqualTo("/rerank");
        await Assert.That(request.Body).IsEqualTo("""{"query":"query","texts":["doc a","doc b"]}""");
    }

    [Test]
    public async Task RerankAsync_SortsByScoreDescendingAndAppliesTopN()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new object[]
                {
                    new { index = 0, score = 0.2 },
                    new { index = 1, score = 0.9 },
                    new { index = 2, score = 0.5 },
                }),
            }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://tei-rerank.test") };
        var reranker = new TeiReranker(client);

        var result = await reranker.RerankAsync("query", ["a", "b", "c"], topN: 2, CancellationToken.None);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0]).IsEqualTo(new RerankResult(1, 0.9));
        await Assert.That(result[1]).IsEqualTo(new RerankResult(2, 0.5));
    }
}
