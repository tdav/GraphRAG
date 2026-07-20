using GraphRag;
using GraphRag.Chunking;
using GraphRag.Indexing.Runtime;
using ManagedCode.GraphRag.Tests.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManagedCode.GraphRag.Tests.Runtime;

public sealed class ServiceCollectionExtensionsTests
{
    [Test]
    public async Task AddGraphRag_RegistersCoreServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IChatClient>(new TestChatClientFactory().CreateClient());
        services.AddGraphRag();
        using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IChunkerResolver>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<PipelineExecutor>()).IsNotNull();
    }
}
