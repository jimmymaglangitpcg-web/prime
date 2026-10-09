using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Prime.Infrastructure.Persistence.HealthChecks;

/// <summary>
/// Reports whether the connection to a remote database verifies the server's certificate
/// (docs/analysis/production-hardening.md §4.3, Q12). <c>SSL Mode=Require</c> encrypts but accepts any certificate, so a
/// network attacker could impersonate the database; <c>VerifyFull</c> with the provider's root certificate does not.
/// A local database (development) is reported healthy. The check reads only the connection string.
/// </summary>
public sealed class DatabaseTlsHealthCheck(string connectionString) : IHealthCheck
{
    private static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "::1"];

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var host = builder.Host ?? string.Empty;
        if (LocalHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            return Task.FromResult(HealthCheckResult.Healthy("Local database: certificate verification not required."));
        }
        return Task.FromResult(builder.SslMode switch
        {
            SslMode.VerifyFull => HealthCheckResult.Healthy("The database certificate and host name are verified."),
            SslMode.VerifyCA => HealthCheckResult.Degraded("The database certificate is verified but not its host name; use SSL Mode=VerifyFull."),
            _ => HealthCheckResult.Degraded($"SSL Mode={builder.SslMode} does not verify the database certificate; use SSL Mode=VerifyFull with the provider's root certificate (docs/DEPLOYMENT.md)."),
        });
    }
}
