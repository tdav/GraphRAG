namespace MyGraphRagV5.Tests;

public class SmokeTests
{
    [Test]
    public async Task ProjectBuilds()
    {
        var builds = 1 + 1 == 2;
        await Assert.That(builds).IsTrue();
    }
}
