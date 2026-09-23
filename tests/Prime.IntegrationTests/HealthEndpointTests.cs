using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Proves the Phase 2 pipeline end to end: the WebApi host builds (DI
/// container resolves, middleware pipeline wires up) and /health returns a
/// well-formed report. Deliberately does NOT require a live PostgreSQL
/// connection to pass — the ASP.NET Core health-check middleware catches
/// exceptions per-check and reports Unhealthy (503) rather than throwing,
/// so this test is meaningful in CI/dev environments with or without a
/// reachable database, while still proving the endpoint and JSON shape
/// work.
/// </summary>
public class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_ReturnsWellFormedReport_RegardlessOfDatabaseAvailability()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("status", out _).ShouldBeTrue();
        json.RootElement.TryGetProperty("checks", out var checks).ShouldBeTrue();
        checks.EnumerateArray().ShouldContain(check =>
            check.GetProperty("name").GetString() == "postgresql");
    }
}
