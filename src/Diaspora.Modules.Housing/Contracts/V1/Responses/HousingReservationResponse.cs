namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>A reservation, with its history. Visible only to the two parties.</summary>
public sealed record HousingReservationResponse
{
    public required Guid Id { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public string? ListingSlug { get; init; }

    public required Guid LandlordId { get; init; }

    public required string LandlordName { get; init; }

    public required Guid SeekerUserId { get; init; }

    /// <summary>Requested, Reserved, LeaseSigned, Occupied, Cancelled, Declined or Expired.</summary>
    public required string Status { get; init; }

    public required int Units { get; init; }

    public required ReservationTermsResponse Terms { get; init; }

    /// <summary>The viewing this followed from, where there was one.</summary>
    public Guid? ViewingId { get; init; }

    public string? Message { get; init; }

    public string? ClosureReason { get; init; }

    /// <summary>Whether this reservation is still keeping units off the market.</summary>
    public required bool HoldsUnits { get; init; }

    public DateTimeOffset? AcceptedAt { get; init; }

    public DateTimeOffset? LeaseSignedAt { get; init; }

    public DateTimeOffset? OccupiedAt { get; init; }

    public IReadOnlyList<ReservationHistoryEntryResponse> History { get; init; } = [];

    public required DateTimeOffset CreatedAt { get; init; }
}
