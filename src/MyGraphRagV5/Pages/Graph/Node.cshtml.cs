using GraphRag.Graphs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;
using MyGraphRagV5.Query;

namespace MyGraphRagV5.Pages.Graph;

/// <summary>
/// Node detail: label, properties, and outgoing relationships for one graph node id.
/// <see cref="IGraphStore"/> has no direct "get node by id" lookup, so the node itself is found by
/// scanning <c>GetNodesAsync</c> and stopping at the first id match; outgoing relationships use the
/// direct, non-scanning <c>GetOutgoingRelationshipsAsync(id)</c>.
/// ponytail: the full scan is fine at the dev/demo scale this viewer targets. Upgrade to a real
/// GetNodeAsync(id) on IGraphStore (a library change, out of 8c's scope) if this becomes a hot path
/// on large graphs.
/// </summary>
public sealed class NodeModel(AppDbContext db, IProjectGraphStoreProvider graphStoreProvider) : PageModel
{
    private readonly AppDbContext db = db;
    private readonly IProjectGraphStoreProvider graphStoreProvider = graphStoreProvider;

    [BindProperty(SupportsGet = true)]
    public Guid ProjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    public RagProject? Project { get; private set; }

    public GraphNode? Node { get; private set; }

    public IReadOnlyList<GraphRelationship> OutgoingRelationships { get; private set; } = [];

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.Id))
        {
            return this.NotFound();
        }

        this.Project = await this.db.RagProjects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == this.ProjectId, cancellationToken);
        if (this.Project is null)
        {
            return this.NotFound();
        }

        try
        {
            var store = await this.graphStoreProvider.GetStoreAsync(this.Project.GraphName, cancellationToken);

            await foreach (var node in store.GetNodesAsync(cancellationToken: cancellationToken))
            {
                if (string.Equals(node.Id, this.Id, StringComparison.Ordinal))
                {
                    this.Node = node;
                    break;
                }
            }

            var relationships = new List<GraphRelationship>();
            await foreach (var relationship in store.GetOutgoingRelationshipsAsync(this.Id, cancellationToken))
            {
                relationships.Add(relationship);
            }

            this.OutgoingRelationships = relationships;
        }
        catch (Exception ex)
        {
            this.Error = ex.Message;
        }

        return this.Page();
    }
}
