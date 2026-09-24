using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Infrastructure.Identity;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests.Forms;

/// <summary>"Today" is the LGU's local date, not the server's UTC date (IClock).</summary>
public class LguClockTests
{
    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static LguClock Clock(DateTimeOffset utcNow, string zone = "Asia/Manila") =>
        new(Options.Create(new LguOptions { TimeZone = zone }), new FixedTime(utcNow));

    [Fact]
    public void Today_IsLocalDate_WhenUtcIsStillThePreviousDay() =>
        Clock(new DateTimeOffset(2026, 9, 24, 22, 30, 0, TimeSpan.Zero)).Today.ShouldBe(new DateOnly(2026, 9, 25)); // 06:30 in Manila

    [Fact]
    public void Today_MatchesUtcDate_LaterInTheDay() =>
        Clock(new DateTimeOffset(2026, 9, 25, 3, 0, 0, TimeSpan.Zero)).Today.ShouldBe(new DateOnly(2026, 9, 25));

    [Fact]
    public void MissingTimeZone_FailsLoudly() =>
        Should.Throw<InvalidOperationException>(() => Clock(DateTimeOffset.UtcNow, zone: ""));
}
