using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;

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
/// </summary>
public class AppUserProvisioningMiddleware(RequestDelegate next, ILogger<AppUserProvisioningMiddleware> logger)
{
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
                        DisplayName = context.User.FindFirstValue(ClaimTypes.Name) ?? "Unknown User",
                        Email = context.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                        Status = RecordStatus.Active,
                    };
                    db.AppUsers.Add(appUser);
                    await db.SaveChangesAsync();
                    logger.LogInformation("Provisioned new AppUser {AppUserId} for Supabase user {SupabaseUserId}", appUser.Id, supabaseUserId);
                }

                currentUser.AppUserId = appUser.Id;
            }
            else
            {
                logger.LogWarning("Authenticated request had no parseable NameIdentifier claim — CurrentUserService.AppUserId will be null.");
            }
        }

        await next(context);
    }
}
