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
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddControllers()
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

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

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
app.UseAuthorization();
app.UseMiddleware<AppUserProvisioningMiddleware>();

app.MapControllers();

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
