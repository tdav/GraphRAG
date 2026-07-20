using GraphRag.Logging;
using MyGraphRagV5.Indexing;

namespace MyGraphRagV5.Tests.Indexing;

public class RunRegistryTests
{
    [Test]
    public async Task TryRegister_RegistersRun_AndExposesHandle()
    {
        var registry = new RunRegistry();
        var runId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        var handle = registry.TryRegister(runId, projectId, cts);

        await Assert.That(handle).IsNotNull();
        await Assert.That(handle!.RunId).IsEqualTo(runId);
        await Assert.That(handle.ProjectId).IsEqualTo(projectId);
        await Assert.That(registry.HasActiveRun(projectId)).IsTrue();
        await Assert.That(registry.Get(runId)).IsSameReferenceAs(handle);
    }

    [Test]
    public async Task TryRegister_RejectsSecondRunForSameProject()
    {
        var registry = new RunRegistry();
        var projectId = Guid.NewGuid();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        var first = registry.TryRegister(Guid.NewGuid(), projectId, cts1);
        var second = registry.TryRegister(Guid.NewGuid(), projectId, cts2);

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNull();
    }

    [Test]
    public async Task Remove_FreesProjectSlot_AllowingANewRun()
    {
        var registry = new RunRegistry();
        var projectId = Guid.NewGuid();
        var firstRunId = Guid.NewGuid();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        registry.TryRegister(firstRunId, projectId, cts1);
        registry.Remove(firstRunId);

        await Assert.That(registry.HasActiveRun(projectId)).IsFalse();
        await Assert.That(registry.Get(firstRunId)).IsNull();
        await Assert.That(registry.TryRegister(Guid.NewGuid(), projectId, cts2)).IsNotNull();
    }

    [Test]
    public async Task CancelRun_CancelsTheRunToken()
    {
        var registry = new RunRegistry();
        var runId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        registry.TryRegister(runId, Guid.NewGuid(), cts);

        var cancelled = registry.CancelRun(runId);

        await Assert.That(cancelled).IsTrue();
        await Assert.That(cts.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task CancelRun_ReturnsFalseForUnknownRun()
    {
        var registry = new RunRegistry();

        await Assert.That(registry.CancelRun(Guid.NewGuid())).IsFalse();
    }

    [Test]
    public async Task CancelRun_ReturnsFalseForAlreadyRemovedRun()
    {
        var registry = new RunRegistry();
        var runId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        registry.TryRegister(runId, Guid.NewGuid(), cts);
        registry.Remove(runId);

        await Assert.That(registry.CancelRun(runId)).IsFalse();
    }

    [Test]
    public async Task CancelRun_HandlesCtsDisposedConcurrentlyWithFinish_WithoutThrowing()
    {
        // Simulates the race in IndexingService.RunPipelineAsync's finally block: the background task
        // can dispose the run's CTS between CancelRun's TryGetValue and its Cancel() call, once the
        // handle is still registered but the token source is already gone.
        var registry = new RunRegistry();
        var runId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        registry.TryRegister(runId, Guid.NewGuid(), cts);
        cts.Dispose();

        var cancelled = registry.CancelRun(runId);

        await Assert.That(cancelled).IsFalse();
    }

    [Test]
    public async Task Update_StoresLatestWorkflowAndProgress_Retrievable()
    {
        var registry = new RunRegistry();
        var runId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        registry.TryRegister(runId, Guid.NewGuid(), cts);
        var sink = (IRunProgressSink)registry;

        sink.Update(runId, "create_communities", null);
        sink.Update(runId, null, new ProgressSnapshot("Embedding", 10, 4));

        var progress = registry.GetProgress(runId);

        await Assert.That(progress).IsNotNull();
        await Assert.That(progress!.CurrentWorkflow).IsEqualTo("create_communities");
        await Assert.That(progress.Progress!.CompletedItems).IsEqualTo(4);
        await Assert.That(progress.Percent!.Value).IsEqualTo(40d);
    }

    [Test]
    public async Task GetProgress_ReturnsNullForUnknownRun()
    {
        var registry = new RunRegistry();

        await Assert.That(registry.GetProgress(Guid.NewGuid())).IsNull();
    }
}
