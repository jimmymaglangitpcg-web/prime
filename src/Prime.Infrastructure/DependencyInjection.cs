using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Common;
using Prime.Application.Features.Billing;
using Prime.Application.Features.Valuation;
using Prime.Infrastructure.Documents;
using Prime.Infrastructure.GIS;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Jobs;
using Prime.Infrastructure.Persistence;
using Prime.Infrastructure.Persistence.HealthChecks;
using Prime.Infrastructure.Persistence.Concurrency;
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
        services.AddScoped<ISecurityEventLog, SecurityEventLog>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        // If-Match of the request (docs/analysis/production-hardening.md §4.4).
        services.AddScoped<ConcurrencyExpectation>();
        services.AddScoped<IConcurrencyExpectation>(sp => sp.GetRequiredService<ConcurrencyExpectation>());
        services.AddScoped<ConcurrencyExpectationInterceptor>();
        // Jurisdiction of the request (docs/analysis/province-wide-operation.md §3.3); PrimeDbContext's query filters read it.
        services.AddScoped<JurisdictionState>();
        services.AddScoped<IJurisdiction>(sp => sp.GetRequiredService<JurisdictionState>());

        services.AddDbContext<PrimeDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite());
            options.AddInterceptors(
                ConcurrencyMaterializationInterceptor.Instance,
                serviceProvider.GetRequiredService<ConcurrencyExpectationInterceptor>(),
                serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
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

        // Legal parameters of the valuation engine (docs/DOMAIN-MODEL.md §3.9). Required,
        // with their citation, so a deployment cannot silently run without them.
        services.AddOptions<ValuationOptions>()
            .Bind(configuration.GetSection(ValuationOptions.SectionName))
            .Validate(o => o.MachineryMinimumRemainingValuePercent is >= 0m and <= 100m,
                "Valuation:MachineryMinimumRemainingValuePercent is required (0-100).")
            .Validate(o => !string.IsNullOrWhiteSpace(o.MachineryMinimumRemainingValueLegalBasis),
                "Valuation:MachineryMinimumRemainingValueLegalBasis is required.")
            .Validate(o => o.MachineryMaximumYearlyDepreciationPercent is null
                    || o.MachineryMaximumYearlyDepreciationPercent is > 0m and <= 100m && !string.IsNullOrWhiteSpace(o.MachineryMaximumYearlyDepreciationLegalBasis),
                "Valuation:MachineryMaximumYearlyDepreciationPercent must be above 0 and at most 100, with Valuation:MachineryMaximumYearlyDepreciationLegalBasis.")
            .Validate(o => o.BackTaxYearsLimit is null || o.BackTaxYearsLimit is >= 0 and <= 100 && !string.IsNullOrWhiteSpace(o.BackTaxYearsLimitLegalBasis),
                "Valuation:BackTaxYearsLimit must be 0 to 100, with Valuation:BackTaxYearsLimitLegalBasis.")
            .Validate(o => o.MarketValueRoundingStep is null || o.MarketValueRoundingStep > 0 && !string.IsNullOrWhiteSpace(o.MarketValueRoundingLegalBasis),
                "Valuation:MarketValueRoundingStep must be positive and needs Valuation:MarketValueRoundingLegalBasis.")
            .ValidateOnStart();

        // Billing engine policy choices (docs/BILLING.md §5); each bill freezes the value used.
        // Statutory notice periods (LGC §§223, 226), each with its citation; required so none is assumed.
        services.AddOptions<Prime.Application.Features.Notices.NoticeOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.Notices.NoticeOptions.SectionName))
            .Validate(o => o.IssuePeriodDays is > 0 && !string.IsNullOrWhiteSpace(o.IssuePeriodLegalBasis),
                "Notices:IssuePeriodDays (> 0) and Notices:IssuePeriodLegalBasis are required.")
            .Validate(o => o.AppealPeriodDays is > 0 && !string.IsNullOrWhiteSpace(o.AppealPeriodLegalBasis),
                "Notices:AppealPeriodDays (> 0) and Notices:AppealPeriodLegalBasis are required.")
            .ValidateOnStart();
        services.AddOptions<BillingOptions>().Bind(configuration.GetSection(BillingOptions.SectionName));

        // MRPAAO forms model (docs/analysis/mrpaao-forms-model.md §6): FAAS = TD + its assessment; unit PIN postscripts.
        services.AddOptions<Prime.Application.Features.Assessments.AssessmentOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.Assessments.AssessmentOptions.SectionName));
        services.AddOptions<Prime.Application.Features.TaxDeclarations.FaasOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.TaxDeclarations.FaasOptions.SectionName));
        services.AddOptions<Prime.Application.Features.PropertyIdentification.PinOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.PropertyIdentification.PinOptions.SectionName))
            .Validate(o => o.BarangayIndexDigits is 3 or 4, "Pin:BarangayIndexDigits must be 3 or 4.")
            .ValidateOnStart();
        services.AddOptions<Prime.Application.Features.RealPropertyUnits.UnitPinOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.RealPropertyUnits.UnitPinOptions.SectionName))
            .Validate(o => o.SuffixStart.Values.All(v => v > 0), "UnitPin:SuffixStart values must be positive.")
            .ValidateOnStart();
        services.AddOptions<Prime.Application.Features.Exemptions.ExemptionsOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.Exemptions.ExemptionsOptions.SectionName))
            .Validate(o => o.ProofPeriodDays > 0, "Exemptions:ProofPeriodDays must be positive.")
            .ValidateOnStart();
        services.AddOptions<Prime.Application.Features.Smv.SmvPreparationOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.Smv.SmvPreparationOptions.SectionName));
        services.AddOptions<Prime.Application.Features.SmvSimulations.ValuationTestingOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.SmvSimulations.ValuationTestingOptions.SectionName));
        services.AddOptions<Prime.Application.Features.GeneralRevision.GeneralRevisionOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.GeneralRevision.GeneralRevisionOptions.SectionName))
            .Validate(o => o.CalamitySuspensionDays > 0, "GeneralRevision:CalamitySuspensionDays must be positive.")
            .Validate(o => o.RollWaitDays >= 0, "GeneralRevision:RollWaitDays cannot be negative.")
            .ValidateOnStart();
        services.AddOptions<Prime.Application.Features.Transactions.DiscoveryOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.Transactions.DiscoveryOptions.SectionName))
            .Validate(o => o.SummonsPeriodDays > 0 && !string.IsNullOrWhiteSpace(o.SummonsLegalBasis),
                "Discovery:SummonsPeriodDays must be positive and Discovery:SummonsLegalBasis given.")
            .ValidateOnStart();
        services.AddOptions<Prime.Application.Features.Registers.RegistersOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.Registers.RegistersOptions.SectionName))
            .Validate(o => o.AssessmentRollRowsPerPage >= 0, "Registers:AssessmentRollRowsPerPage cannot be negative.")
            .ValidateOnStart();
        services.AddOptions<Prime.Application.Features.Reports.ReportsOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.Reports.ReportsOptions.SectionName))
            .Validate(o => o.SyncRowLimit > 0 && o.FileRetentionDays > 0, "Reports:SyncRowLimit and Reports:FileRetentionDays must be positive.")
            .ValidateOnStart();
        services.AddScoped<Prime.Application.Features.Reports.IFaasInForceQuery, Reporting.FaasInForceQuery>();
        services.AddSingleton<Prime.Application.Features.Reports.IReportFileWriter, Reporting.CsvReportWriter>();
        services.AddSingleton<Prime.Application.Features.Reports.IReportFileWriter, Reporting.ExcelReportWriter>();

        // Forms foundation (docs/FORMS-REVISION-PLAN.md): LGU branding, numbering, rendering, provisional forms.
        services.AddOptions<LguOptions>().Bind(configuration.GetSection(LguOptions.SectionName));
        services.AddOptions<Prime.Application.Features.Forms.FormsOptions>()
            .Bind(configuration.GetSection(Prime.Application.Features.Forms.FormsOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, LguClock>();
        services.AddScoped<INumberSequenceAllocator, NumberSequenceAllocator>();
        services.AddScoped<ICollectionLock, CollectionLock>();
        services.AddSingleton<IFormRenderer, FluidFormRenderer>();
        services.AddHostedService<ProvisionalFormSeeder>();
        // Permission catalogue and its provisional default grants (docs/analysis/workflow-security.md §4.1).
        services.AddHostedService<Prime.Infrastructure.Identity.PermissionCatalogSeeder>();

        // LGU content packs (docs/analysis/lgu-content-pack.md): read from the configured, gitignored content root.
        services.AddOptions<Prime.Infrastructure.ContentPacks.ContentPackOptions>()
            .Bind(configuration.GetSection(Prime.Infrastructure.ContentPacks.ContentPackOptions.SectionName))
            .Validate(o => o.MaxFileBytes > 0, "ContentPacks:MaxFileBytes must be positive.")
            .ValidateOnStart();
        services.AddScoped<IContentPackSource, Prime.Infrastructure.ContentPacks.FileSystemContentPackSource>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
            .AddCheck<PostGisHealthCheck>("postgis", tags: ["ready"])
            .AddCheck<RowLevelSecurityHealthCheck>("row-level-security", tags: ["ready"])
            .AddCheck<BackgroundJobsHealthCheck>("background-jobs", tags: ["ready"])
            .AddCheck("database-tls", new DatabaseTlsHealthCheck(connectionString));

        // Background jobs (CLAUDE.md §33/§72 General Revision; ARCHITECTURE.md
        // §3.8). Packages were referenced since Phase 2 and deliberately left
        // unwired until Phase 6 actually needed them.
        // Sliding invisibility timeout: without it the storage hands a job that has run for 30 minutes to another worker
        // while the first is still running, so a province-wide revision ran twice at once (production-hardening.md §9, H4).
        // The job is handed on only when its server stops renewing it.
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions { UseSlidingInvisibilityTimeout = true }));
        services.AddHangfireServer();
        services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();

        return services;
    }
}
