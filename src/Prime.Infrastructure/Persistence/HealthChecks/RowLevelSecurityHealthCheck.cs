using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Prime.Infrastructure.Persistence.HealthChecks;

/// <summary>
/// On Supabase, every <c>public</c> table must have row-level security on and no policy, so the project's anon and
/// authenticated keys read nothing through its REST and GraphQL APIs; PRIME reaches the database only through its own
/// connection (docs/analysis/workflow-security.md §4.4). Supabase's <c>ensure_rls</c> event trigger turns RLS on for new
/// tables; this check catches a table it missed, and any policy someone added. PostGIS's <c>spatial_ref_sys</c>
/// (public reference data, owned by the extension) is the one listed exception. A database without Supabase's
/// <c>anon</c> role (local development) has no such API, and passes.
/// </summary>
public class RowLevelSecurityHealthCheck(PrimeDbContext db) : IHealthCheck
{
    public static readonly string[] Exceptions = ["spatial_ref_sys"];

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var supabase = await db.Database.SqlQuery<bool>($"""SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') AS "Value" """)
                .SingleAsync(cancellationToken);
            if (!supabase)
            {
                return HealthCheckResult.Healthy("No Supabase API roles on this database.");
            }
            var open = await db.Database.SqlQuery<string>($"""
                SELECT c.relname AS "Value" FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p') AND NOT c.relrowsecurity AND c.relname <> ALL({Exceptions})
                ORDER BY 1
                """).ToListAsync(cancellationToken);
            var policies = await db.Database.SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM pg_policies WHERE schemaname = 'public'""")
                .SingleAsync(cancellationToken);
            return open.Count == 0 && policies == 0
                ? HealthCheckResult.Healthy("Row-level security on every public table, with no policies.")
                : HealthCheckResult.Unhealthy(
                    $"Supabase API exposure: {open.Count} public table(s) without row-level security ({string.Join(", ", open.Take(10))}); {policies} policy(ies).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Row-level security could not be checked.", ex);
        }
    }
}
