using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GraphRag.Vectors;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace MyGraphRagV5.Vectors;

/// <summary>
/// pgvector-backed <see cref="IVectorStore"/>. Each collection gets its own table
/// ("vec_{sanitized collection name}"), created lazily on first upsert - the embedding
/// dimension is fixed by whatever vector is written first.
/// </summary>
public sealed class PgVectorStore(NpgsqlDataSource dataSource) : IVectorStore, IDisposable
{
    private readonly NpgsqlDataSource dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    private readonly ConcurrentDictionary<string, byte> ensuredTables = new();

    public async Task UpsertAsync(
        string collection,
        ReadOnlyMemory<float> embedding,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(metadata);

        var table = PgVectorNaming.TableName(collection);
        await this.EnsureTableAsync(table, embedding.Length, cancellationToken).ConfigureAwait(false);

        var id = PgVectorNaming.ResolveId(metadata);
        var text = PgVectorNaming.ResolveText(metadata);
        var metadataJson = JsonSerializer.Serialize(metadata);

        await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {table} (id, embedding, text, metadata, updated_at)
            VALUES ($1, $2, $3, $4, now())
            ON CONFLICT (id) DO UPDATE SET
                embedding = EXCLUDED.embedding,
                text = EXCLUDED.text,
                metadata = EXCLUDED.metadata,
                updated_at = now();
            """;
        command.Parameters.Add(new NpgsqlParameter { Value = id });
        command.Parameters.Add(new NpgsqlParameter { Value = new Vector(embedding) });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)text ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = metadataJson });

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<VectorSearchResult> SearchAsync(
        string collection,
        ReadOnlyMemory<float> embedding,
        int limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);

        var table = PgVectorNaming.TableName(collection);
        if (!this.ensuredTables.ContainsKey(table))
        {
            // Nobody ever upserted into this collection, so the table doesn't exist yet.
            yield break;
        }

        await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT id, text, metadata, 1 - (embedding <=> $1) AS score
            FROM {table}
            ORDER BY embedding <=> $1
            LIMIT $2;
            """;
        command.Parameters.Add(new NpgsqlParameter { Value = new Vector(embedding) });
        command.Parameters.Add(new NpgsqlParameter { Value = limit });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetString(0);
            var score = reader.GetDouble(3);

            // ponytail: System.Text.Json boxes jsonb object values as JsonElement; a richer
            // conversion to native CLR types can be added later if a caller needs it.
            var metadata = reader.IsDBNull(2)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object?>>(reader.GetString(2));

            yield return new VectorSearchResult(id, score, metadata);
        }
    }

    public void Dispose() => this.dataSource.Dispose();

    private async Task EnsureTableAsync(string table, int dimension, CancellationToken cancellationToken)
    {
        if (this.ensuredTables.ContainsKey(table))
        {
            return;
        }

        await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using (var createTable = connection.CreateCommand())
        {
            createTable.CommandText = $"""
                CREATE TABLE IF NOT EXISTS {table} (
                    id text PRIMARY KEY,
                    embedding vector({dimension}),
                    text text,
                    metadata jsonb,
                    updated_at timestamptz DEFAULT now()
                );
                """;
            await createTable.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var createIndex = connection.CreateCommand())
        {
            createIndex.CommandText = $"""
                CREATE INDEX IF NOT EXISTS {table}_embedding_hnsw_idx
                    ON {table} USING hnsw (embedding vector_cosine_ops);
                """;
            await createIndex.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        this.ensuredTables.TryAdd(table, 0);
    }
}
