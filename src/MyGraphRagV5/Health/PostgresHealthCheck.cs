using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace MyGraphRagV5.Health;

/// <summary>
/// Opens a short-lived <see cref="NpgsqlConnection"/> to verify the database is reachable.
/// Hand-rolled instead of pulling in a health-check NuGet package since Npgsql is already
/// a project dependency and the check is a few lines.
/// </summary>
public sealed class PostgresHealthCheck(string connectionString) : IHealthCheck
{
    private readonly string connectionString = connectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Postgres connection opened.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres unreachable.", ex);
        }
    }
}
