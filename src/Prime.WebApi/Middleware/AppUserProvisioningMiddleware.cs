using System.Diagnostics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Prime.WebApi.Contracts;

namespace Prime.WebApi.Middleware;

/// <summary>
/// Runs after authentication, before controllers. Resolves the
/// authenticated principal's Supabase user id (or the Development bypass's
/// fake id — same claim shape either way, see
/// DevelopmentAuthenticationHandler) to a local AppUser row, creating one
/// on first sight ("just-in-time" provisioning — Supabase Auth owns
/// sign-up; PRIME only needs a matching profile/authorization row).
/// Populates the request-scoped CurrentUserService so downstream handlers
/// and the audit interceptor know who is acting, without every handler
/// re-deriving it from claims.
/// A new user starts Pending: they may only ask for an account until a
/// SYSTEM_ADMIN approves it. A disabled user is refused here, on every
/// request, whatever their token (docs/analysis/workflow-security.md §4.2).
/// </summary>
public class AppUserProvisioningMiddleware(RequestDelegate next, ILogger<AppUserProvisioningMiddleware> logger)
{
    /// <summary>Supabase Auth's authenticator assurance level claim (aal1, or aal2 after a second factor).</summary>
    public const string AssuranceLevelClaim = "aal";

    public async Task InvokeAsync(HttpContext context, PrimeDbContext db, CurrentUserService currentUser)
    {
        currentUser.IpAddress = context.Connection.RemoteIpAddress?.ToString();

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subjectClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (Guid.TryParse(subjectClaim, out var supabaseUserId))
            {
                var appUser = await db.AppUsers.FirstOrDefaultAsync(u => u.SupabaseUserId == supabaseUserId);

                if (appUser is null)
                {
                    appUser = new AppUser
                    {
                        SupabaseUserId = supabaseUserId,
                        DisplayName = context.User.FindFirstValue(ClaimTypes.Name) ?? context.User.FindFirstValue(ClaimTypes.Email) ?? "Unknown User",
                        Email = context.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                        Status = AppUserStatus.Pending,
                    };
                    db.AppUsers.Add(appUser);
                    await db.SaveChangesAsync();
                    logger.LogInformation("Provisioned new pending AppUser {AppUserId} for Supabase user {SupabaseUserId}", appUser.Id, supabaseUserId);
                }

                if (appUser.Status == AppUserStatus.Inactive)
                {
                    logger.LogWarning("Refused a request from disabled AppUser {AppUserId}", appUser.Id);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new ApiError("USER_DISABLED", "Your PRIME account has been disabled. Ask a system administrator.",
                        null, Activity.Current?.Id ?? context.TraceIdentifier));
                    return;
                }

                currentUser.AppUserId = appUser.Id;
                currentUser.AssuranceLevel = context.User.FindFirstValue(AssuranceLevelClaim);
            }
            else
            {
                logger.LogWarning("Authenticated request had no parseable NameIdentifier claim — CurrentUserService.AppUserId will be null.");
            }
        }

        await next(context);
    }
}
