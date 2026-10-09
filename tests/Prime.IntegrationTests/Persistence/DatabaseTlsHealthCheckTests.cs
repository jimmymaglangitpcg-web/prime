using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prime.Infrastructure.Persistence.HealthChecks;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests.Persistence;

/// <summary>The database-tls check reads only the connection string (production-hardening.md §4.3, Q12).</summary>
public class DatabaseTlsHealthCheckTests
{
    [Theory]
    [InlineData("Host=localhost;Database=prime_dev;Username=prime", HealthStatus.Healthy)]
    [InlineData("Host=db.example.invalid;Database=postgres;SSL Mode=VerifyFull;Root Certificate=ca.crt", HealthStatus.Healthy)]
    [InlineData("Host=db.example.invalid;Database=postgres;SSL Mode=VerifyCA", HealthStatus.Degraded)]
    [InlineData("Host=db.example.invalid;Database=postgres;SSL Mode=Require", HealthStatus.Degraded)]
    // The shape of a Supabase pooler string that encrypts without verifying (DEMO values).
    [InlineData("Host=pooler.example.invalid;Port=6543;Database=postgres;Username=u;Password=p;Max Auto Prepare=0;No Reset On Close=true;SSL Mode=Require;Trust Server Certificate=true", HealthStatus.Degraded)]
    [InlineData("Host=db.example.invalid;Database=postgres", HealthStatus.Degraded)]
    public async Task Reports_whether_a_remote_database_certificate_is_verified(string connectionString, HealthStatus expected)
    {
        var result = await new DatabaseTlsHealthCheck(connectionString).CheckHealthAsync(new HealthCheckContext());
        result.Status.ShouldBe(expected);
    }
}
