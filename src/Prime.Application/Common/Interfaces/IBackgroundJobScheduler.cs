using System.Linq.Expressions;

namespace Prime.Application.Common.Interfaces;

/// <summary>
/// Abstracts background job enqueueing (CLAUDE.md §33/§72/§73) behind an
/// Application-owned interface, the same way <see cref="IApplicationDbContext"/>
/// abstracts EF Core — Application must not reference Hangfire directly
/// (Clean Architecture dependency direction, docs/ARCHITECTURE.md §2).
/// Implemented in Prime.Infrastructure by wrapping Hangfire's own
/// <c>IBackgroundJobClient</c>.
/// </summary>
public interface IBackgroundJobScheduler
{
    void Enqueue<T>(Expression<Func<T, Task>> methodCall);
}
