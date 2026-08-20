namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// How a provider is available at all, before any particular day is considered.
/// <para>
/// The weekly schedule answers "when is it open"; this answers the prior question of whether
/// "open" is even the right idea. An emergency plumber is not open or closed, they are
/// reachable; a consultant with no shop is neither, they take appointments. The client's
/// <c>isOpenNow</c> boolean silently assumes <see cref="BusinessHours"/> for everyone, which is
/// why every mock provider has one hard-coded.
/// </para>
/// <para>
/// This is what <c>AvailabilityCalculator</c> switches on first: for
/// <see cref="AppointmentOnly"/> and <see cref="ByArrangement"/> it does not compute an
/// open-now answer at all and returns null, so the client renders "By appointment" rather than
/// a misleading "Closed" outside office hours.
/// </para>
/// </summary>
public enum AvailabilityKind
{
    /// <summary>A weekly schedule decides. The default, and what every client mock assumes.</summary>
    BusinessHours = 1,

    /// <summary>Always. A 24-hour pharmacy, an online service.</summary>
    AlwaysOpen = 2,

    /// <summary>Only by appointment. Open-now is not a meaningful question.</summary>
    AppointmentOnly = 3,

    /// <summary>Callable at any hour for urgent work. Out-of-hours contact is expected.</summary>
    EmergencyService = 4,

    /// <summary>Ad hoc — the provider and the member agree a time. Community groups, tutors.</summary>
    ByArrangement = 5,

    /// <summary>Not currently available at all. Distinct from a paused provider: the provider is
    /// listed and operating, but this particular thing is not on offer right now.</summary>
    Unavailable = 6
}
