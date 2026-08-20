namespace Diaspora.Modules.Providers.Extensibility.Availability;

/// <summary>
/// What an availability provider concluded.
/// <para>
/// <b>Three states, not two, and the third is the important one.</b> The client's
/// <c>isOpenNow</c> is a boolean, which forces every provider that has no meaningful open-now
/// answer — an appointment-only consultant, a provider that has not filled in its hours — to be
/// rendered as "Closed". That is wrong in the direction that loses the provider business, and it
/// is why <see cref="IsOpen"/> is nullable here and all the way out to the wire.
/// </para>
/// <para>
/// <see cref="NextChangeAt"/> is what turns "Open now" into "Open now — closes at 20:00", which
/// is materially more useful and costs nothing extra to compute once the schedule is loaded.
/// </para>
/// </summary>
public sealed record AvailabilityAnswer
{
    /// <summary>True, false, or null when "open" is not a meaningful question.</summary>
    public bool? IsOpen { get; init; }

    /// <summary>Whether this provider had an opinion. False hands back to the next provider.</summary>
    public bool IsDecided { get; init; } = true;

    /// <summary>When the answer next changes — closing time, or the next opening.</summary>
    public DateTimeOffset? NextChangeAt { get; init; }

    /// <summary>
    /// A short phrase the client may render instead of "Open"/"Closed". "By appointment",
    /// "Closed for Eid", "Fully booked tonight". Written by whoever knows why.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>The module that answered, for telemetry and for debugging a surprising answer.</summary>
    public string? Source { get; init; }

    /// <summary>No opinion. Hands back to the next provider, and ultimately to the schedule.</summary>
    public static AvailabilityAnswer Unknown { get; } = new() { IsDecided = false };

    public static AvailabilityAnswer Open(
        string source, DateTimeOffset? closesAt = null, string? reason = null) =>
        new() { IsOpen = true, NextChangeAt = closesAt, Source = source, Reason = reason };

    public static AvailabilityAnswer Closed(
        string source, DateTimeOffset? opensAt = null, string? reason = null) =>
        new() { IsOpen = false, NextChangeAt = opensAt, Source = source, Reason = reason };

    /// <summary>Decided, and the decision is that the question does not apply.</summary>
    public static AvailabilityAnswer NotApplicable(string source, string? reason = null) =>
        new() { IsOpen = null, Source = source, Reason = reason };
}
