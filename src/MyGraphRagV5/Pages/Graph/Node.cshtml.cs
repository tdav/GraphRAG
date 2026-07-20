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
/// ponytail: the scan is capped at <see cref="MaxNodeScan"/> nodes (via GraphTraversalOptions.Take,
/// which bounds the underlying SQL query itself) -- it is never unbounded, even for a missing id.
/// A node beyond the cap will not be found. Upgrade to a real GetNodeAsync(id) on IGraphStore (a
/// library change, out of 8c's scope) if that becomes a problem.
/// </summary>
public sealed class NodeModel(AppDbContext db, IProjectGraphStoreProvider graphStoreProvider) : PageModel
{
    /// <summary>Hard cap on how many nodes the id lookup will scan before giving up.</summary>
    public const int MaxNodeScan = 5000;

    private readonly AppDbContext db = db;
    private readonly IProjectGraphStoreProvider graphStoreProvider = graphStoreProvider;

    [BindProperty(SupportsGet = true)]
    public Guid ProjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    public RagProject? Project { get; private set; }

    public GraphNode? Node { get; private set; }

    public IReadOnlyList<GraphRelationship> OutgoingRelationships { get; private set; } = [];

    /// <summary>True when the node was not found because the scan hit <see cref="MaxNodeScan"/>, not because it genuinely doesn't exist.</summary>
    public bool ScanCapReached { get; private set; }

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

            var scanned = 0;
            await foreach (var node in store.GetNodesAsync(new GraphTraversalOptions { Take = MaxNodeScan }, cancellationToken))
            {
                scanned++;
                if (string.Equals(node.Id, this.Id, StringComparison.Ordinal))
                {
                    this.Node = node;
                    break;
                }
            }

            this.ScanCapReached = this.Node is null && scanned >= MaxNodeScan;

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
