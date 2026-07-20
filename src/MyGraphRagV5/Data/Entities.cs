namespace MyGraphRagV5.Data;

public sealed class RagProject
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required string SourceFolder { get; init; }

    public required string GraphName { get; init; }

    public required string VectorCollection { get; init; }

    public string FilePattern { get; init; } = @".*\.(cs|md)$";

    public int? EmbeddingDimension { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public enum IndexingRunStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

public sealed class IndexingRun
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public IndexingRunStatus Status { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? CurrentWorkflow { get; init; }

    public double? ProgressPercent { get; init; }

    public string? Error { get; init; }

    public int? DocumentCount { get; init; }
}

public sealed class ChatSession
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public required string Title { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ChatMessage
{
    public Guid Id { get; init; }

    public Guid SessionId { get; init; }

    public required string Role { get; init; }

    public required string Content { get; init; }

    public string? SourcesJson { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
