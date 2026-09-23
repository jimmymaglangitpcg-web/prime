using System.Linq.Expressions;
using Hangfire;
using Prime.Application.Common.Interfaces;

namespace Prime.Infrastructure.Jobs;

public sealed class HangfireBackgroundJobScheduler(IBackgroundJobClient client) : IBackgroundJobScheduler
{
    public void Enqueue<T>(Expression<Func<T, Task>> methodCall) => client.Enqueue(methodCall);
}
