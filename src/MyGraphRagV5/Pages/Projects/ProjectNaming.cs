using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;
using MyGraphRagV5.Vectors;

namespace MyGraphRagV5.Pages.Projects;

/// <summary>
/// Derives a unique (GraphName, VectorCollection) pair from a project's display name at Create
/// time. Neither is ever changed afterwards (see Edit page) because the AGE graph and pgvector
/// data already indexed under those names would otherwise be orphaned.
/// </summary>
public static class ProjectNaming
{
    /// <summary>
    /// GraphName is passed verbatim to <c>ProjectGraphStoreProvider.GetStoreAsync</c> as the AGE
    /// graph identifier, so it is sanitized and "g_"-prefixed here (safe identifier, never starts
    /// with a digit). VectorCollection is the logical collection <c>IVectorStore</c> sanitizes again
    /// into its own "vec_"-prefixed table name (<see cref="PgVectorNaming.TableName"/>), so it is
    /// stored unprefixed here to avoid ending up with a "vec_vec_..." table. Both share the same
    /// disambiguating suffix so a GraphName collision (the only DB-enforced unique index) also keeps
    /// VectorCollection unique in practice.
    /// </summary>
    public static async Task<(string GraphName, string VectorCollection)> DeriveUniqueAsync(
        AppDbContext db, string projectName, CancellationToken cancellationToken)
    {
        var slug = PgVectorNaming.SanitizeCollectionName(projectName);
        if (slug.Trim('_').Length == 0)
        {
            slug = "project";
        }

        var candidate = slug;
        var suffix = 0;
        while (await db.RagProjects.AnyAsync(p => p.GraphName == $"g_{candidate}", cancellationToken).ConfigureAwait(false))
        {
            suffix++;
            candidate = $"{slug}_{suffix}";
        }

        return ($"g_{candidate}", candidate);
    }
}
