namespace Prime.Application.Common.Interfaces;

/// <summary>
/// The LGU's calendar date and time. Server clocks run in UTC, but "today"
/// for effective dates, rules in force and document dates is the LGU's local
/// date (configured as <c>Lgu:TimeZone</c>). Early in a Philippine morning the
/// UTC date is still the previous day, so never use
/// <c>DateOnly.FromDateTime(DateTime.UtcNow)</c> for business dates.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Today's date in the LGU's time zone.</summary>
    DateOnly Today { get; }

    /// <summary>The LGU-local calendar date of <paramref name="instant"/> (e.g. the day an approval happened).</summary>
    DateOnly LocalDate(DateTimeOffset instant);
}
