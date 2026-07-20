namespace MyGraphRagV5.Vectors;

/// <summary>
/// Pure helpers used by <see cref="PgVectorStore"/> to turn a logical collection name and
/// upsert metadata into the identifiers/values it needs. Kept separate so they are unit
/// testable without a database.
/// </summary>
public static class PgVectorNaming
{
    /// <summary>
    /// Lowercases and replaces anything outside [a-z0-9_] with '_'. Different inputs can
    /// collide onto the same sanitized name (e.g. "My Col" and "my-col") - callers are
    /// expected to pick collection names that don't rely on those characters for uniqueness.
    /// </summary>
    public static string SanitizeCollectionName(string collection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);

        var sanitized = new char[collection.Length];
        for (var i = 0; i < collection.Length; i++)
        {
            var c = char.ToLowerInvariant(collection[i]);
            sanitized[i] = c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' ? c : '_';
        }

        return new string(sanitized);
    }

    public static string TableName(string collection) => $"vec_{SanitizeCollectionName(collection)}";

    public static string ResolveId(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue("id", out var value) && value is not null
            ? value.ToString() ?? Guid.NewGuid().ToString()
            : Guid.NewGuid().ToString();

    public static string? ResolveText(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue("text", out var value) ? value?.ToString() : null;
}
