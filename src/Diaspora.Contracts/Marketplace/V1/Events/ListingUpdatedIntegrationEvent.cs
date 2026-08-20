using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A listing's content changed.
/// <para>
/// <see cref="ChangedFields"/> is what makes this usable rather than merely correct: a price cut
/// matters to everybody watching the listing, and a caption edit matters to the search index and
/// to nobody else. A consumer that could only see "something changed" would either notify on
/// everything or on nothing.
/// </para>
/// <para>
/// <b>The new price travels with the event when the price is what moved.</b> Price-watch
/// notifications are the single most-requested marketplace feature, and making every consumer read
/// the listing back to find out what it now costs would put the whole watchlist fan-out through
/// this module on every edit.
/// </para>
/// </summary>
public sealed record ListingUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public Guid? ChangedBy { get; init; }

    /// <summary>
    /// Short field names: <c>title</c>, <c>description</c>, <c>price</c>, <c>stock</c>,
    /// <c>location</c>, <c>fulfillment</c>, <c>attributes</c>, <c>tags</c>, <c>category</c>.
    /// </summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }

    /// <summary>The price as it now stands, when <c>price</c> is among the changed fields.</summary>
    public decimal? PriceAmount { get; init; }

    /// <summary>The price before the change, so a consumer can say "reduced from".</summary>
    public decimal? PreviousPriceAmount { get; init; }

    public string? CurrencyCode { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
