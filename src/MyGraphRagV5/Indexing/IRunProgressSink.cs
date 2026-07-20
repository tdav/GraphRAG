using GraphRag.Logging;

namespace MyGraphRagV5.Indexing;

/// <summary>
/// Run-scoped sink that receives indexing progress updates. Implemented by the run
/// registry (see Task 6); this interface is the seam RunProgressCallbacks writes through.
/// </summary>
public interface IRunProgressSink
{
    void Update(Guid runId, string? currentWorkflow, ProgressSnapshot? progress);
}
