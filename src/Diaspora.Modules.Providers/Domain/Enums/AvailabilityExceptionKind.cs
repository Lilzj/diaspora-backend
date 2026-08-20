namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// Why a dated override departs from the weekly schedule.
/// <para>
/// Kept as a kind rather than folded into a single "closed on this date" flag because the
/// client will render them differently and a member reads them differently. "Closed for Eid"
/// is information; "Closed" is an inconvenience. The kind is also what lets a future
/// scheduling module decide whether to offer to rebook — you rebook around a holiday, you do
/// not rebook around a permanent change of hours.
/// </para>
/// </summary>
public enum AvailabilityExceptionKind
{
    /// <summary>A public or religious holiday. Usually closed, sometimes shortened.</summary>
    Holiday = 1,

    /// <summary>Shut for a stated period. Refurbishment, illness, bereavement.</summary>
    TemporaryClosure = 2,

    /// <summary>Open, but not at the usual times. Late-night opening, a half day.</summary>
    SpecialHours = 3,

    /// <summary>Annual leave.</summary>
    Vacation = 4,

    /// <summary>Open specially on a day the weekly schedule has closed.</summary>
    SpecialOpening = 5
}
