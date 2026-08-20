using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// When a member wants their collected messages.
/// <para>
/// Expressed in the member's own wall clock for the same reason quiet hours are: "8am" is a
/// statement about breakfast, not about UTC. The sweep converts, once, at the moment it runs.
/// </para>
/// </summary>
public sealed class DigestSchedule : ValueObject
{
    private DigestSchedule(DigestFrequency frequency, int hourOfDay, DayOfWeek dayOfWeek)
    {
        Frequency = frequency;
        HourOfDay = hourOfDay;
        DayOfWeek = dayOfWeek;
    }

    private DigestSchedule() { }

    public DigestFrequency Frequency { get; private set; }

    /// <summary>Local hour, 0–23.</summary>
    public int HourOfDay { get; private set; }

    /// <summary>Only meaningful for a weekly digest. Ignored otherwise, never null.</summary>
    public DayOfWeek DayOfWeek { get; private set; }

    /// <summary>
    /// The default: daily at 8am local.
    /// <para>
    /// On rather than off, because the alternative to a digest is not silence — it is every low
    /// priority message arriving on its own, which is the behaviour members leave over.
    /// </para>
    /// </summary>
    public static DigestSchedule Default() => new(DigestFrequency.Daily, 8, System.DayOfWeek.Monday);

    public static DigestSchedule Off() => new(DigestFrequency.Off, 8, System.DayOfWeek.Monday);

    public static DigestSchedule? TryCreate(DigestFrequency frequency, int hourOfDay, DayOfWeek dayOfWeek)
    {
        if (hourOfDay is < 0 or > 23)
        {
            return null;
        }

        if (!Enum.IsDefined(dayOfWeek))
        {
            return null;
        }

        return new DigestSchedule(frequency, hourOfDay, dayOfWeek);
    }

    public bool IsActive => Frequency is DigestFrequency.Daily or DigestFrequency.Weekly;

    /// <summary>
    /// Whether the schedule fires at this local moment.
    /// <para>
    /// Hour granularity, matching the sweep's cadence. A schedule that fired on the minute would
    /// need a sweep that ran every minute to be honoured, and a digest is not worth that.
    /// </para>
    /// </summary>
    public bool FiresAt(DateTimeOffset localNow) => Frequency switch
    {
        DigestFrequency.Daily => localNow.Hour == HourOfDay,
        DigestFrequency.Weekly => localNow.Hour == HourOfDay && localNow.DayOfWeek == DayOfWeek,
        _ => false
    };

    /// <summary>How far back the digest reaches. The window, not the schedule.</summary>
    public TimeSpan Window => Frequency == DigestFrequency.Weekly
        ? TimeSpan.FromDays(7)
        : TimeSpan.FromDays(1);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Frequency;
        yield return HourOfDay;
        yield return DayOfWeek;
    }
}
