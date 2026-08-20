using Diaspora.Modules.Analytics.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>
/// The one place a moment becomes a calendar day.
/// <para>
/// <b>Every day boundary on this deployment is cut here and nowhere else.</b> §10 of the phase
/// brief forbids analytics depending on server local time, and the way to keep that promise is to
/// have exactly one component that knows what "a day" means — so a handler cannot reach for
/// <c>DateTime.Now</c>, and a test can move the boundary without moving the machine.
/// </para>
/// <para>
/// <b>The zone is resolved once and cached.</b> <c>TimeZoneInfo.FindSystemTimeZoneById</c> reads
/// the platform's zone database on every call; doing that per analytics row would put a file-system
/// lookup on the ingestion path.
/// </para>
/// </summary>
public sealed class AnalyticsReportingCalendar
{
    private readonly IClock _clock;
    private readonly Lazy<TimeZoneInfo> _zone;

    public AnalyticsReportingCalendar(IClock clock, IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _zone = new Lazy<TimeZoneInfo>(() => Resolve(options.Value.ReportingTimeZone));
    }

    /// <summary>The deployment's reporting zone, resolved.</summary>
    public TimeZoneInfo Zone => _zone.Value;

    /// <summary>The zone's IANA identifier, as served on every response.</summary>
    public string TimeZoneId => Zone.Id;

    public DateTimeOffset UtcNow => _clock.UtcNow;

    /// <summary>The calendar day an instant falls in, in the reporting zone.</summary>
    public DateOnly DayOf(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    /// <summary>Today, in the reporting zone.</summary>
    public DateOnly Today => DayOf(UtcNow);

    /// <summary>
    /// Reads a caller-supplied IANA name, returning null when it is not one this host knows.
    /// <para>
    /// Null rather than throwing, because an unknown zone from a client is an ordinary event — an
    /// old device with a stale zone database — and the caller turns it into a named refusal.
    /// </para>
    /// </summary>
    public static TimeZoneInfo? TryResolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    /// <summary>
    /// The configured zone, falling back to UTC.
    /// <para>
    /// <b>Falls back rather than failing the host, and that is a deliberate asymmetry.</b> A
    /// deployment whose zone database does not carry the configured name should still serve
    /// analytics — in UTC, which every response states — rather than refuse to start. The
    /// alternative turns a packaging difference between two container images into an outage.
    /// </para>
    /// </summary>
    private static TimeZoneInfo Resolve(string timeZoneId) => TryResolve(timeZoneId) ?? TimeZoneInfo.Utc;
}
