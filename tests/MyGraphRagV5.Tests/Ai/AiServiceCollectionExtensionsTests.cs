using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyGraphRagV5.Ai;

namespace MyGraphRagV5.Tests.Ai;

public class AiServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAiClients_ResolvesIChatClientWithoutApiKeyConfigured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tei:EmbedUrl"] = "http://tei-embed.test",
                ["Tei:RerankUrl"] = "http://tei-rerank.test",
            })
            .Build();

        var services = new ServiceCollection().AddAiClients(config);
        using var provider = services.BuildServiceProvider();

        var chatClient = provider.GetService<IChatClient>();

        Assert.NotNull(chatClient);
    }
}
