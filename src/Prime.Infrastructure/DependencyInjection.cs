using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Infrastructure.GIS;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Jobs;
using Prime.Infrastructure.Persistence;
using Prime.Infrastructure.Persistence.HealthChecks;
using Prime.Infrastructure.Persistence.Interceptors;

namespace Prime.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPrimeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PrimeDb")
            ?? throw new InvalidOperationException(
                "Connection string 'PrimeDb' is not configured. Set ConnectionStrings:PrimeDb " +
                "(local Postgres for dev, Supabase's pooled connection for staging/prod — see docs/DATABASE.md §1.1).");

        // Scoped (not singleton): both the interceptor and the middleware
        // that populates it must share the same instance within a request,
        // and AppUserId differs per request/user.
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<PrimeDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite());
            options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<PrimeDbContext>());

        // GIS measurement (docs/GIS.md §2). MeasurementSrid is optional —
        // absent means geodesic area on WGS84 — but when set it must be a
        // positive EPSG code; an unknown code fails loudly in PostGIS rather
        // than silently producing degree-based areas.
        services.AddOptions<GisOptions>()
            .Bind(configuration.GetSection(GisOptions.SectionName))
            .Validate(o => o.MeasurementSrid is null or > 0, "Gis:MeasurementSrid must be a positive EPSG code when set.")
            .ValidateOnStart();
        services.AddScoped<IGeometryMeasurementService, GeometryMeasurementService>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
            .AddCheck<PostGisHealthCheck>("postgis", tags: ["ready"]);

        // Background jobs (CLAUDE.md §33/§72 General Revision; ARCHITECTURE.md
        // §3.8). Packages were referenced since Phase 2 and deliberately left
        // unwired until Phase 6 actually needed them.
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer();
        services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();

        return services;
    }
}
