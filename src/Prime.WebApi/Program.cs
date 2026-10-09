using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Prime.Application;
using Prime.Infrastructure;
using Prime.WebApi.Authentication;
using Prime.WebApi.Middleware;
using Prime.WebApi.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Every action declares its permission; one that does not is refused (docs/analysis/workflow-security.md §4.1).
builder.Services.AddControllers(options =>
    {
        options.Conventions.Add(new Prime.WebApi.Authorization.PermissionDeclarationConvention());
        options.Filters.Add<Prime.WebApi.Concurrency.IfMatchFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Enums serialize/deserialize as strings ("Active", "Individual",
        // ...) rather than raw numbers — found necessary by actually
        // driving the frontend against the real API in a browser: without
        // this, every response would have shown "status": 0 instead of
        // "Active", and the frontend (which sends string enum values, as
        // any sane JSON API consumer would) got a 400 on every enum field.
        // allowIntegerValues defaults to true, so this stays compatible
        // with anything still sending/expecting numeric enum values.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "PRIME API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT bearer token issued by Supabase Auth (or the local dev bypass in Development).",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = [],
    });
});

builder.Services.AddPrimeApplication();

builder.Services.AddPrimeInfrastructure(builder.Configuration);

// --- Authentication -----------------------------------------------------
// Development: a local bypass simulating an authenticated Supabase user, so
// work isn't blocked on a live Supabase project (docs/ARCHITECTURE.md §3.4).
// Every other environment validates real Supabase-issued JWTs.
var useDevAuthBypass = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("DevAuth:Enabled");

if (useDevAuthBypass)
{
    builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
        .AddScheme<DevelopmentAuthOptions, DevelopmentAuthenticationHandler>(
            DevelopmentAuthenticationHandler.SchemeName,
            options => builder.Configuration.GetSection("DevAuth").Bind(options));
    // DEMO offices and office assignments for the DEMO users (docs/analysis/province-wide-operation.md Q11, Q13).
    builder.Services.AddHostedService<DevOfficeSeeder>();
}
else
{
    // Supabase Auth publishes standard OIDC discovery at
    // {SupabaseUrl}/auth/v1/.well-known/openid-configuration, which points
    // at its JWKS endpoint. Using Authority lets JwtBearer fetch and cache
    // signing keys automatically, including rotation — no secret to manage
    // here at all (confirmed against the actual project: it uses the newer
    // asymmetric JWT Signing Keys, ES256, not the legacy shared secret).
    var supabaseUrl = builder.Configuration["Supabase:Url"];

    // The Auth project settings PRIME relies on, checked at start-up and on /health (workflow-security.md §4.2 Q8).
    builder.Services.AddHttpClient(Prime.Infrastructure.Identity.SupabaseAuthSettingsCheck.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(10));
    builder.Services.AddSingleton<Prime.Infrastructure.Identity.SupabaseAuthSettingsCheck>();
    builder.Services.AddHealthChecks().AddCheck<Prime.Infrastructure.Identity.SupabaseAuthSettingsCheck>("supabase-auth");
    builder.Services.AddHostedService<Prime.Infrastructure.Identity.SupabaseAuthSettingsStartupCheck>();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = $"{supabaseUrl}/auth/v1";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{supabaseUrl}/auth/v1",
                ValidateAudience = true,
                ValidAudience = "authenticated",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
            };
        });
}

// Sign-in settings: MFA-required roles and the browser's idle sign-out (docs/analysis/workflow-security.md §4.2).
builder.Services.AddSingleton(builder.Configuration.GetSection("Security").Get<Prime.Application.Common.Security.SecuritySettings>()
    ?? new Prime.Application.Common.Security.SecuritySettings());

builder.Services.AddAuthorization();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Prime.WebApi.Authorization.PermissionAuthorizationHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, Prime.WebApi.Authorization.PermissionResultHandler>();

// Hardening (docs/analysis/workflow-security.md §4.4): request limits, and security headers outside Development.
var rateLimits = builder.Configuration.GetSection("RateLimits").Get<RateLimitSettings>() ?? new RateLimitSettings();
builder.Services.AddPrimeRateLimiting(rateLimits, rateLimits.Enabled ?? !builder.Environment.IsDevelopment());
var sendSecurityHeaders = builder.Configuration.GetValue<bool?>("Security:Headers") ?? !builder.Environment.IsDevelopment();

// Behind a reverse proxy, the client's address (for the audit log and the per-IP limit) and the HTTPS scheme come
// from the proxy's forwarded headers, trusted only from the addresses listed here.
var knownProxies = builder.Configuration.GetSection("Security:KnownProxies").Get<string[]>() ?? [];
if (knownProxies.Length > 0)
{
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var proxy in knownProxies)
        {
            options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
        }
    });
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// One-off set-up of a new installation's first SYSTEM_ADMIN; never reachable through the API (workflow-security.md §4.2).
if (args is [Prime.WebApi.Commands.BootstrapAdminCommand.Name, var bootstrapEmail, ..])
{
    Environment.ExitCode = await Prime.WebApi.Commands.BootstrapAdminCommand.RunAsync(app.Services, bootstrapEmail);
    return;
}

// Development only: the DEMO volume set for the performance measurements (production-hardening.md §4.5).
if (args is [Prime.WebApi.Commands.GenerateVolumeCommand.Name, ..])
{
    Environment.ExitCode = await Prime.WebApi.Commands.GenerateVolumeCommand.RunAsync(app.Configuration, app.Environment, args);
    return;
}

// Development only: the DEMO set the browser end-to-end suite works on (production-hardening.md §4.7).
if (args is [Prime.WebApi.Commands.SeedE2eCommand.Name, ..])
{
    Environment.ExitCode = await Prime.WebApi.Commands.SeedE2eCommand.RunAsync(app.Configuration, app.Environment);
    return;
}

if (knownProxies.Length > 0)
{
    app.UseForwardedHeaders();
}
if (sendSecurityHeaders)
{
    app.UseMiddleware<SecurityHeadersMiddleware>();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Hangfire's default dashboard authorization allows all requests —
    // fine for local development, but CLAUDE.md §67 requires real
    // authorization before this is ever exposed outside Development.
    // DOMAIN VERIFICATION REQUIRED before enabling in any other environment.
    app.UseHangfireDashboard();

    if (useDevAuthBypass)
    {
        Log.Warning("DevAuth bypass is ENABLED — every request is treated as an authenticated user. Development environment only.");
    }
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
// After authentication, so a signed-in user is limited as themselves rather than by address.
app.UseRateLimiter();
// The user and their office are resolved before authorization, which reads their roles' permissions (workflow-security.md §4.1).
app.UseMiddleware<AppUserProvisioningMiddleware>();
app.UseMiddleware<JurisdictionMiddleware>();
app.UseAuthorization();

app.MapControllers();

if (useDevAuthBypass)
{
    // Development only: the DEMO users the header switch can act as (Q13).
    app.MapGet("/api/dev/users", (Microsoft.Extensions.Options.IOptionsMonitor<DevelopmentAuthOptions> options) =>
            options.Get(DevelopmentAuthenticationHandler.SchemeName).EffectiveUsers.Select(u => new { u.Key, u.DisplayName, u.Office, u.Roles }))
        .RequireAuthorization();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
            }),
        });
        await context.Response.WriteAsync(payload);
    },
});

app.Run();

// Exposed for WebApplicationFactory<Program> in Prime.IntegrationTests.
public partial class Program;
