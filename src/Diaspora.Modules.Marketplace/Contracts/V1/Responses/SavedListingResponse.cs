namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// One entry in a member's watchlist.
/// <para>
/// Carries the listing card rather than only its identifier, because the watchlist screen is a list
/// of items and not a list of references — and because the interesting thing about a saved listing
/// is exactly what has changed on it since it was saved.
/// </para>
/// </summary>
public sealed record SavedListingResponse
{
    public required Guid SavedListingId { get; init; }

    public required ListingCardResponse Listing { get; init; }

    /// <summary>The member's own private note.</summary>
    public string? Note { get; init; }

    public required bool NotifyOnPriceChange { get; init; }

    public required DateTimeOffset SavedAt { get; init; }
}
