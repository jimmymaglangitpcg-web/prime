using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Prime.Infrastructure.Persistence.HealthChecks;

/// <summary>
/// Confirms the postgis extension is actually enabled and queryable on the
/// connected database, not just that Postgres itself is reachable
/// (AddNpgSql already covers plain connectivity). Per CLAUDE.md §70
/// ("/health ... Check: ... GIS/PostGIS").
/// </summary>
public class PostGisHealthCheck(PrimeDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var version = await dbContext.Database
                .SqlQuery<string>($"SELECT postgis_version() AS \"Value\"")
                .SingleAsync(cancellationToken);

            return HealthCheckResult.Healthy($"PostGIS {version}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostGIS extension is not available on this database.", ex);
        }
    }
}
