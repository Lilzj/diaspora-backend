namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// What sort of observance a public holiday entry records.
/// <para>
/// The kind is not the same question as what closes — a religious holiday may close nothing and a
/// national one may close everything, and in several countries the reverse is true. Closures are
/// therefore recorded as their own flags on the entry, and this only says what the day <em>is</em>.
/// </para>
/// </summary>
public enum HolidayKind
{
    /// <summary>Observed nationwide.</summary>
    National = 0,

    /// <summary>Observed in part of the country. The region is named on the entry.</summary>
    Regional = 1,

    /// <summary>A religious observance, whether or not it is a statutory holiday.</summary>
    Religious = 2,

    /// <summary>A closure of a diplomatic mission that is not a national holiday.</summary>
    EmbassyClosure = 3,

    /// <summary>A government office closure that is not a national holiday.</summary>
    GovernmentClosure = 4,

    /// <summary>
    /// A working day moved rather than granted — the Saturday a country works to bridge a holiday.
    /// Recorded because somebody planning an appointment needs it as much as the holiday itself.
    /// </summary>
    WorkingDayAdjustment = 5
}
