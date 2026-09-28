using Prime.Application.Features.Offices;
using Prime.Infrastructure.Identity;

namespace Prime.WebApi.Middleware;

/// <summary>
/// Runs after <see cref="AppUserProvisioningMiddleware"/>: limits the request
/// to the acting user's jurisdiction (docs/analysis/province-wide-operation.md §3.3).
/// A municipal user works only in the municipalities their office covers
/// today; a user with no office in force sees no records; provincial and
/// province-wide users are not limited.
/// </summary>
public class JurisdictionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentUserService currentUser, IOfficeContext offices, JurisdictionState jurisdiction)
    {
        if (currentUser.AppUserId is not null)
        {
            var scope = await offices.GetAsync(context.RequestAborted);
            if (!scope.ProvinceWide)
            {
                jurisdiction.Restrict(scope.MunicipalityIds ?? []);
            }
        }
        await next(context);
    }
}
