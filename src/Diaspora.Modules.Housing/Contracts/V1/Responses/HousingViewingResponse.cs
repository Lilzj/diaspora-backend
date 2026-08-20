namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// A viewing, with its history.
/// <para>
/// Visible only to the two parties. Staff get no bypass: reading a named person appointments is a
/// different act from moderating a public listing, and it should require a deliberate support tool
/// rather than fall out of a role check.
/// </para>
/// </summary>
public sealed record HousingViewingResponse
{
    public required Guid Id { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public string? ListingSlug { get; init; }

    public required Guid LandlordId { get; init; }

    public required string LandlordName { get; init; }

    public required Guid SeekerUserId { get; init; }

    /// <summary>Requested, Scheduled, Completed, Cancelled, NoShow or Expired.</summary>
    public required string Status { get; init; }

    public DateTimeOffset? PreferredAt { get; init; }

    public DateTimeOffset? ConfirmedAt { get; init; }

    public required int DurationMinutes { get; init; }

    /// <summary>A video walk-through rather than a visit.</summary>
    public required bool IsVirtual { get; init; }

    /// <summary>Where to meet, or how to join. Never the property address.</summary>
    public string? Place { get; init; }

    public string? Message { get; init; }

    /// <summary>On a no-show, which party failed to appear.</summary>
    public bool? LandlordFailedToAttend { get; init; }

    public string? ClosureReason { get; init; }

    /// <summary>Whether this viewing entitles the member to the full address.</summary>
    public required bool UnlocksAddress { get; init; }

    /// <summary>Whether it satisfies a landlord viewing-before-reservation requirement.</summary>
    public required bool SatisfiesReservationRequirement { get; init; }

    public IReadOnlyList<ViewingHistoryEntryResponse> History { get; init; } = [];

    public required DateTimeOffset CreatedAt { get; init; }
}
