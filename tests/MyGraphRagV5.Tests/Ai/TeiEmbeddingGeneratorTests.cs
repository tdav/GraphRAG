using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MyGraphRagV5.Ai;

namespace MyGraphRagV5.Tests.Ai;

public class TeiEmbeddingGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_PostsInputsToEmbedEndpointAndMapsResponseInOrder()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { new[] { 0.1f, 0.2f }, new[] { 0.3f, 0.4f } }),
            }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://tei-embed.test") };
        var generator = new TeiEmbeddingGenerator(client, Options.Create(new TeiOptions { EmbedBatchSize = 16 }));

        var result = await generator.GenerateAsync(["a", "b"]);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Request.Method);
        Assert.Equal("/embed", request.Request.RequestUri!.AbsolutePath);
        Assert.Equal("""{"inputs":["a","b"]}""", request.Body);

        Assert.Equal(2, result.Count);
        Assert.Equal([0.1f, 0.2f], result[0].Vector.ToArray());
        Assert.Equal([0.3f, 0.4f], result[1].Vector.ToArray());
        Assert.Equal(2, generator.Dimension);
    }

    [Fact]
    public async Task GenerateAsync_SplitsInputsLargerThanEmbedBatchSizeIntoMultipleRequests()
    {
        var handler = new StubHttpMessageHandler(async request =>
        {
            var payload = await request.Content!.ReadFromJsonAsync<Dictionary<string, string[]>>();
            var vectors = payload!["inputs"].Select(_ => new[] { 1f }).ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(vectors) };
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://tei-embed.test") };
        var generator = new TeiEmbeddingGenerator(client, Options.Create(new TeiOptions { EmbedBatchSize = 2 }));

        var result = await generator.GenerateAsync(["a", "b", "c", "d", "e"]);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("""{"inputs":["a","b"]}""", handler.Requests[0].Body);
        Assert.Equal("""{"inputs":["c","d"]}""", handler.Requests[1].Body);
        Assert.Equal("""{"inputs":["e"]}""", handler.Requests[2].Body);
        Assert.Equal(5, result.Count);
    }
}
