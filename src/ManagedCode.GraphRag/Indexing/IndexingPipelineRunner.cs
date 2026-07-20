using GraphRag.Cache;
using GraphRag.Callbacks;
using GraphRag.Config;
using GraphRag.Indexing.Runtime;
using GraphRag.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GraphRag.Indexing;

public sealed class IndexingPipelineRunner(IServiceProvider services, IPipelineFactory pipelineFactory, PipelineExecutor executor)
{
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly IPipelineFactory _pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
    private readonly PipelineExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    public Task<IReadOnlyList<PipelineRunResult>> RunAsync(GraphRagConfig config, CancellationToken cancellationToken = default)
    {
        return RunCoreAsync(config, NoopWorkflowCallbacks.Instance, cancellationToken);
    }

    public Task<IReadOnlyList<PipelineRunResult>> RunAsync(GraphRagConfig config, IWorkflowCallbacks callbacks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callbacks);
        return RunCoreAsync(config, callbacks, cancellationToken);
    }

    private async Task<IReadOnlyList<PipelineRunResult>> RunCoreAsync(GraphRagConfig config, IWorkflowCallbacks callbacks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);

        var pipelineDescriptor = IndexingPipelineDefinitions.Standard;
        var pipeline = _pipelineFactory.BuildIndexingPipeline(pipelineDescriptor);

        var inputStorage = PipelineStorageFactory.Create(config.Input.Storage);
        var outputStorage = PipelineStorageFactory.Create(config.Output);
        var previousStorage = PipelineStorageFactory.Create(config.UpdateIndexOutput);
        var cache = _services.GetService<IPipelineCache>();
        var stats = new PipelineRunStats();
        var state = new PipelineState();

        var context = new PipelineRunContext(
            inputStorage,
            outputStorage,
            previousStorage,
            cache,
            callbacks,
            stats,
            state,
            _services);

        var results = new List<PipelineRunResult>();
        await foreach (var result in _executor.ExecuteAsync(pipeline, config, context, cancellationToken).ConfigureAwait(false))
        {
            results.Add(result);
        }

        return results;
    }
}
