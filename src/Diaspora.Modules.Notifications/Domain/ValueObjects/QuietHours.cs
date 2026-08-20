using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// The window a member does not want to be interrupted in, expressed in their own wall clock.
/// <para>
/// <b>Whole hours in the member's time zone, and both halves of that are load-bearing.</b> Whole
/// hours because "22:00 to 07:00" is how a person describes when they sleep and nobody has ever
/// wanted 22:15. The member's own zone because the platform's members are, by definition, spread
/// across them — a Lagos member and a Toronto member who both say "10pm" mean two instants five
/// hours apart, and storing UTC would make one of them wrong twice a year when the other's clocks
/// change.
/// </para>
/// <para>
/// <b>Windows that wrap midnight are the normal case, not the edge case.</b> Start 22, end 7 means
/// 22:00–23:59 and 00:00–06:59. A containment check written as <c>hour >= start &amp;&amp; hour &lt;
/// end</c> is silently false for every night-time window a real person configures, which is the
/// defect <see cref="Contains"/> exists to make impossible.
/// </para>
/// </summary>
public sealed class QuietHours : ValueObject
{
    private QuietHours(bool isEnabled, int startHour, int endHour)
    {
        IsEnabled = isEnabled;
        StartHour = startHour;
        EndHour = endHour;
    }

    private QuietHours() { }

    public bool IsEnabled { get; private set; }

    /// <summary>Inclusive. 0–23.</summary>
    public int StartHour { get; private set; }

    /// <summary>Exclusive. 0–23.</summary>
    public int EndHour { get; private set; }

    /// <summary>Quiet hours switched off. The default for a new member.</summary>
    public static QuietHours Disabled() => new(false, 22, 7);

    /// <summary>
    /// Whether the two hours describe a usable window. Equal hours are refused because they could
    /// equally mean "never" and "always", and a preference that ambiguous is a bug waiting to be
    /// argued about.
    /// </summary>
    public static bool IsValid(int startHour, int endHour) =>
        startHour is >= 0 and <= 23
        && endHour is >= 0 and <= 23
        && startHour != endHour;

    public static QuietHours? TryCreate(bool isEnabled, int startHour, int endHour)
    {
        if (!isEnabled)
        {
            return new QuietHours(false, Clamp(startHour), Clamp(endHour));
        }

        return IsValid(startHour, endHour) ? new QuietHours(true, startHour, endHour) : null;
    }

    /// <summary>
    /// Whether a local hour falls inside the window. Handles the wrap. See the class summary.
    /// </summary>
    public bool Contains(int localHour)
    {
        if (!IsEnabled)
        {
            return false;
        }

        return StartHour < EndHour
            ? localHour >= StartHour && localHour < EndHour
            : localHour >= StartHour || localHour < EndHour;
    }

    /// <summary>
    /// The next instant after <paramref name="localNow"/> at which the window ends.
    /// <para>
    /// Returned as a local wall-clock value the caller converts back with the member's zone. Held
    /// here rather than in the resolver because the wrap arithmetic and the containment test have to
    /// agree, and two implementations of a wrap never do for long.
    /// </para>
    /// </summary>
    public DateTimeOffset NextEndAfter(DateTimeOffset localNow)
    {
        if (!IsEnabled || !Contains(localNow.Hour))
        {
            return localNow;
        }

        var candidate = new DateTimeOffset(
            localNow.Year, localNow.Month, localNow.Day, EndHour, 0, 0, localNow.Offset);

        return candidate <= localNow ? candidate.AddDays(1) : candidate;
    }

    private static int Clamp(int hour) => Math.Clamp(hour, 0, 23);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsEnabled;
        yield return StartHour;
        yield return EndHour;
    }
}
