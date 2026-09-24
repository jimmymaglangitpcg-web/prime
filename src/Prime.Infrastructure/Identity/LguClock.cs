using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;

namespace Prime.Infrastructure.Identity;

/// <summary><see cref="IClock"/> in the time zone configured as <c>Lgu:TimeZone</c> (an IANA id, e.g. "Asia/Manila").</summary>
public sealed class LguClock(IOptions<LguOptions> options, TimeProvider time) : IClock
{
    private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(
        string.IsNullOrWhiteSpace(options.Value.TimeZone)
            ? throw new InvalidOperationException("Lgu:TimeZone is required (an IANA time zone id, e.g. Asia/Manila).")
            : options.Value.TimeZone);

    public DateTimeOffset UtcNow => time.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
}
