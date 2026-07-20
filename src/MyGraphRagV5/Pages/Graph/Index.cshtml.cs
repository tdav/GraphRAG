using GraphRag.Graphs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;
using MyGraphRagV5.Query;

namespace MyGraphRagV5.Pages.Graph;

/// <summary>
/// Paged entities/relationships viewer for a project's AGE graph, via <see cref="IProjectGraphStoreProvider"/>.
/// Communities are not in the AGE graph -- they live in the vector store with no listing API -- so
/// they are out of scope for this viewer (entities + relationships + node detail only).
/// </summary>
public sealed class IndexModel(AppDbContext db, IProjectGraphStoreProvider graphStoreProvider) : PageModel
{
    public const int PageSize = 50;

    private readonly AppDbContext db = db;
    private readonly IProjectGraphStoreProvider graphStoreProvider = graphStoreProvider;

    [BindProperty(SupportsGet = true)]
    public Guid? ProjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int NodeSkip { get; set; }

    [BindProperty(SupportsGet = true)]
    public int RelSkip { get; set; }

    public IReadOnlyList<RagProject> Projects { get; private set; } = [];

    public IReadOnlyList<GraphNode> Nodes { get; private set; } = [];

    public IReadOnlyList<GraphRelationship> Relationships { get; private set; } = [];

    public bool NodesHasMore { get; private set; }

    public bool RelationshipsHasMore { get; private set; }

    /// <summary>Set when the graph store could not be reached/queried; rendered as a friendly panel instead of a 500.</summary>
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        this.NodeSkip = Math.Max(0, this.NodeSkip);
        this.RelSkip = Math.Max(0, this.RelSkip);

        this.Projects = await this.db.RagProjects.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        if (this.ProjectId is null || !this.Projects.Any(p => p.Id == this.ProjectId))
        {
            this.ProjectId = this.Projects.Count > 0 ? this.Projects[0].Id : null;
        }

        if (this.ProjectId is not { } projectId)
        {
            return;
        }

        var project = this.Projects.First(p => p.Id == projectId);

        try
        {
            var store = await this.graphStoreProvider.GetStoreAsync(project.GraphName, cancellationToken);

            // Take PageSize + 1 so we can tell whether a next page exists without a separate count
            // query -- IGraphStore exposes no count API.
            var nodes = new List<GraphNode>();
            await foreach (var node in store.GetNodesAsync(new GraphTraversalOptions { Skip = this.NodeSkip, Take = PageSize + 1 }, cancellationToken))
            {
                nodes.Add(node);
            }

            this.NodesHasMore = nodes.Count > PageSize;
            this.Nodes = nodes.Take(PageSize).ToList();

            var relationships = new List<GraphRelationship>();
            await foreach (var relationship in store.GetRelationshipsAsync(new GraphTraversalOptions { Skip = this.RelSkip, Take = PageSize + 1 }, cancellationToken))
            {
                relationships.Add(relationship);
            }

            this.RelationshipsHasMore = relationships.Count > PageSize;
            this.Relationships = relationships.Take(PageSize).ToList();
        }
        catch (Exception ex)
        {
            this.Error = ex.Message;
        }
    }

    public static string DescriptionOf(IReadOnlyDictionary<string, object?> properties) =>
        properties.TryGetValue("description", out var value) && value is not null ? value.ToString()! : "—";
}
