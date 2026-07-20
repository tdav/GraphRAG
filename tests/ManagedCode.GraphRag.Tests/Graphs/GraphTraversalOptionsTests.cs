using GraphRag.Graphs;

namespace ManagedCode.GraphRag.Tests.Graphs;

public sealed class GraphTraversalOptionsTests
{
    [Test]
    public async Task Validate_AllowsPositiveValues()
    {
        var options = new GraphTraversalOptions { Skip = 5, Take = 10 };
        options.Validate();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Validate_ThrowsForNegativeSkip()
    {
        var options = new GraphTraversalOptions { Skip = -1 };
        await Assert.That(() => options.Validate()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validate_ThrowsForNegativeTake()
    {
        var options = new GraphTraversalOptions { Take = -5 };
        await Assert.That(() => options.Validate()).Throws<ArgumentOutOfRangeException>();
    }
}
