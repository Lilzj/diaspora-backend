namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Adds a dated override of the weekly schedule.
/// <para>
/// A range rather than a single date, because a two-week closure is one statement the provider
/// makes and not fourteen. Both bounds are inclusive, which is what a person means by "closed
/// from the 20th to the 27th".
/// </para>
/// </summary>
public sealed record AvailabilityExceptionRequest
{
    /// <summary>The <c>AvailabilityExceptionKind</c> name — <c>Holiday</c>, <c>TemporaryClosure</c>.</summary>
    public required string Kind { get; init; }

    public required DateOnly StartsOn { get; init; }

    /// <summary>Inclusive. Equal to <see cref="StartsOn"/> for a single day.</summary>
    public required DateOnly EndsOn { get; init; }

    /// <summary>False when the provider is open, but not at the usual times.</summary>
    public bool IsClosed { get; init; } = true;

    /// <summary><c>HH:mm</c>. Required when <see cref="IsClosed"/> is false.</summary>
    public string? OpensAt { get; init; }

    /// <summary><c>HH:mm</c>. Required when <see cref="IsClosed"/> is false.</summary>
    public string? ClosesAt { get; init; }

    /// <summary>What to show the member. "Closed for Eid", "Staff training".</summary>
    public string? Reason { get; init; }
}
