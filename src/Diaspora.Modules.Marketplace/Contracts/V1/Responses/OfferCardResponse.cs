namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A negotiation as a dashboard list renders it.
/// <para>
/// Carries no history, which is what makes a seller with two hundred open negotiations a page
/// rather than a download. The full record is one call away.
/// </para>
/// </summary>
public sealed record OfferCardResponse
{
    public required Guid OfferId { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public Guid? ListingCoverMediaId { get; init; }

    public required string Status { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required int Quantity { get; init; }

    public required string AwaitingParty { get; init; }

    /// <summary>Whether it is the caller's move. What sorts the dashboard.</summary>
    public required bool IsMyTurn { get; init; }

    public required int Rounds { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset LastActivityAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
