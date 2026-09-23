namespace Prime.Application.Common.Interfaces;

/// <summary>
/// The acting user for the current request, resolved once per request from
/// the authenticated principal (Supabase JWT, or the Development auth
/// bypass) to a local <c>AppUser.Id</c>. Read by feature handlers that need
/// to know who is acting (e.g. to set CreatedBy explicitly) and by the
/// audit interceptor. <see cref="AppUserId"/> is null for anonymous
/// requests (e.g. /health).
/// </summary>
public interface ICurrentUserService
{
    Guid? AppUserId { get; }
    string? IpAddress { get; }

    /// <summary>
    /// Optional reason for the current mutation (maker-checker/workflow
    /// actions per CLAUDE.md §48). Feature handlers set this before calling
    /// SaveChanges when a reason is relevant; the audit interceptor reads
    /// it. Null for ordinary create/update of non-workflow entities.
    /// </summary>
    string? Reason { get; set; }
}
