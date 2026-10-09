using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Prime.Infrastructure.Jobs;

/// <summary>
/// Reports whether a Hangfire server is running and how many jobs have failed (CLAUDE.md §70 "background jobs";
/// docs/analysis/production-hardening.md §4.3). No server means general revisions and other queued work never run:
/// Unhealthy. Failed jobs wait for an operator in the Hangfire records: Degraded.
/// </summary>
public class BackgroundJobsHealthCheck(JobStorage storage) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = storage.GetMonitoringApi().GetStatistics();
            var data = new Dictionary<string, object>
            {
                ["servers"] = stats.Servers, ["enqueued"] = stats.Enqueued, ["processing"] = stats.Processing, ["failed"] = stats.Failed,
            };
            var description = $"{stats.Servers} server(s), {stats.Enqueued} enqueued, {stats.Processing} processing, {stats.Failed} failed";
            if (stats.Servers == 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"No background job server is running ({description}).", data: data));
            }
            return Task.FromResult(stats.Failed > 0
                ? HealthCheckResult.Degraded(description, data: data)
                : HealthCheckResult.Healthy(description, data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("The background job storage cannot be read.", ex));
        }
    }
}
