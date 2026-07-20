using GraphRag.Logging;
using MyGraphRagV5.Indexing;

namespace MyGraphRagV5.Tests.Indexing;

public class RunRegistryTests
{
    [Fact]
    public void TryRegister_RegistersRun_AndExposesHandle()
    {
        var registry = new RunRegistry();
        var runId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        var handle = registry.TryRegister(runId, projectId, cts);

        Assert.NotNull(handle);
        Assert.Equal(runId, handle!.RunId);
        Assert.Equal(projectId, handle.ProjectId);
        Assert.True(registry.HasActiveRun(projectId));
        Assert.Same(handle, registry.Get(runId));
    }

    [Fact]
    public void TryRegister_RejectsSecondRunForSameProject()
    {
        var registry = new RunRegistry();
        var projectId = Guid.NewGuid();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        var first = registry.TryRegister(Guid.NewGuid(), projectId, cts1);
        var second = registry.TryRegister(Guid.NewGuid(), projectId, cts2);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void Remove_FreesProjectSlot_AllowingANewRun()
    {
        var registry = new RunRegistry();
        var projectId = Guid.NewGuid();
        var firstRunId = Guid.NewGuid();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        registry.TryRegister(firstRunId, projectId, cts1);
        registry.Remove(firstRunId);

        Assert.False(registry.HasActiveRun(projectId));
        Assert.Null(registry.Get(firstRunId));
        Assert.NotNull(registry.TryRegister(Guid.NewGuid(), projectId, cts2));
    }

    [Fact]
    public void CancelRun_CancelsTheRunToken()
    {
        var registry = new RunRegistry();
        var runId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        registry.TryRegister(runId, Guid.NewGuid(), cts);

        var cancelled = registry.CancelRun(runId);

        Assert.True(cancelled);
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void CancelRun_ReturnsFalseForUnknownRun()
    {
        var registry = new RunRegistry();

        Assert.False(registry.CancelRun(Guid.NewGuid()));
    }

    [Fact]
    public void Update_StoresLatestWorkflowAndProgress_Retrievable()
    {
        var registry = new RunRegistry();
        var runId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        registry.TryRegister(runId, Guid.NewGuid(), cts);
        var sink = (IRunProgressSink)registry;

        sink.Update(runId, "create_communities", null);
        sink.Update(runId, null, new ProgressSnapshot("Embedding", 10, 4));

        var progress = registry.GetProgress(runId);

        Assert.NotNull(progress);
        Assert.Equal("create_communities", progress!.CurrentWorkflow);
        Assert.Equal(4, progress.Progress!.CompletedItems);
        Assert.Equal(40d, progress.Percent!.Value);
    }

    [Fact]
    public void GetProgress_ReturnsNullForUnknownRun()
    {
        var registry = new RunRegistry();

        Assert.Null(registry.GetProgress(Guid.NewGuid()));
    }
}
