using System.Net;
using System.Net.Http.Json;
using MyGraphRagV5.Ai;

namespace MyGraphRagV5.Tests.Ai;

public class TeiRerankerTests
{
    [Fact]
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

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Request.Method);
        Assert.Equal("/rerank", request.Request.RequestUri!.AbsolutePath);
        Assert.Equal("""{"query":"query","texts":["doc a","doc b"]}""", request.Body);
    }

    [Fact]
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

        Assert.Equal(2, result.Count);
        Assert.Equal(new RerankResult(1, 0.9), result[0]);
        Assert.Equal(new RerankResult(2, 0.5), result[1]);
    }
}
