using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MyGraphRagV5.Ai;

namespace MyGraphRagV5.Tests.Ai;

public class TeiEmbeddingGeneratorTests
{
    [Test]
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

        await Assert.That(handler.Requests).HasSingleItem();
        var request = handler.Requests.Single();
        await Assert.That(request.Request.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(request.Request.RequestUri!.AbsolutePath).IsEqualTo("/embed");
        await Assert.That(request.Body).IsEqualTo("""{"inputs":["a","b"]}""");

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Vector.ToArray()).IsEquivalentTo([0.1f, 0.2f]);
        await Assert.That(result[1].Vector.ToArray()).IsEquivalentTo([0.3f, 0.4f]);
        await Assert.That(generator.Dimension).IsEqualTo(2);
    }

    [Test]
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

        await Assert.That(handler.Requests.Count).IsEqualTo(3);
        await Assert.That(handler.Requests[0].Body).IsEqualTo("""{"inputs":["a","b"]}""");
        await Assert.That(handler.Requests[1].Body).IsEqualTo("""{"inputs":["c","d"]}""");
        await Assert.That(handler.Requests[2].Body).IsEqualTo("""{"inputs":["e"]}""");
        await Assert.That(result.Count).IsEqualTo(5);
    }
}
