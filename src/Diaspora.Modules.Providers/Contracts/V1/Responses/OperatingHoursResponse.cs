namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// A provider's schedule for one day of the week.
/// <para>
/// The client's <c>businessHours[]</c> with one shape change: several
/// <see cref="AvailabilityWindowResponse"/> per day instead of one open/close pair, so a split
/// shift is representable and a member is not told a restaurant is open at four in the afternoon.
/// </para>
/// <para>
/// <see cref="IsClosed"/> and an empty <see cref="Windows"/> are <b>different</b>. "Closed on
/// Sundays" is a statement the provider made; no hours recorded for Sunday is an absence of
/// information, and a client should render the first as "Closed" and the second as nothing at
/// all. <see cref="IsSpecified"/> is the flag that tells them apart without the client
/// reasoning about it.
/// </para>
/// </summary>
public sealed record OperatingHoursResponse
{
    /// <summary>"Monday". The client's <c>dayOfWeek</c>, spelled identically.</summary>
    public required string DayOfWeek { get; init; }

    /// <summary>An explicit statement that the provider does not open on this day.</summary>
    public required bool IsClosed { get; init; }

    /// <summary>Whether the provider has said anything at all about this day.</summary>
    public required bool IsSpecified { get; init; }

    public required IReadOnlyList<AvailabilityWindowResponse> Windows { get; init; }

    /// <summary>
    /// The day rendered as one string — "09:00 – 17:00", "Closed". Produced server-side so the
    /// client's organization screen, which stores hours as a pre-formatted string today, needs
    /// no change.
    /// </summary>
    public required string DisplayText { get; init; }
}
