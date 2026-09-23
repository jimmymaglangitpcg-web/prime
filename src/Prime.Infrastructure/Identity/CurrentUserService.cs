using Prime.Application.Common.Interfaces;

namespace Prime.Infrastructure.Identity;

/// <summary>
/// Scoped, request-lifetime holder for the resolved current user. Populated
/// once per request by <see cref="AppUserProvisioningMiddleware"/> (which
/// resolves/creates the AppUser.Id) — feature handlers and the audit
/// interceptor only ever read it via <see cref="ICurrentUserService"/>.
/// The setter is deliberately not part of that interface: only the
/// provisioning middleware resolves this concrete type to populate it.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    public Guid? AppUserId { get; set; }
    public string? IpAddress { get; set; }
    public string? Reason { get; set; }

    public void ActAsForBackgroundJob(Guid? userId) => AppUserId = userId;
}
