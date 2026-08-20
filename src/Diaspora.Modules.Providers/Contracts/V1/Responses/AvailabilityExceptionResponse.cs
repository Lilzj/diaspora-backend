namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// A dated override of the weekly schedule.
/// <para>
/// The brief's holiday overrides and temporary closures. Without them, "open now" is wrong on
/// every public holiday and through every refurbishment — and wrong in the direction that sends
/// a member across London to a locked door.
/// </para>
/// </summary>
public sealed record AvailabilityExceptionResponse
{
    public required Guid ExceptionId { get; init; }

    /// <summary>The <c>AvailabilityExceptionKind</c> name — <c>Holiday</c>, <c>TemporaryClosure</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Inclusive.</summary>
    public required DateOnly StartsOn { get; init; }

    /// <summary>Inclusive. Equal to <see cref="StartsOn"/> for a single day.</summary>
    public required DateOnly EndsOn { get; init; }

    public required bool IsClosed { get; init; }

    /// <summary><c>HH:mm</c>. Set only when <see cref="IsClosed"/> is false.</summary>
    public string? OpensAt { get; init; }

    /// <summary><c>HH:mm</c>. Set only when <see cref="IsClosed"/> is false.</summary>
    public string? ClosesAt { get; init; }

    /// <summary>What to show the member. "Closed for Eid", "Staff training".</summary>
    public string? Reason { get; init; }
}
