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

    // ── 21 Oct 2026: who may be told, and what a saved search can compare. Appended, never inserted.

    /// <summary>
    /// Whether a stranger could find the listing by searching after this change — not deleted, in a
    /// publicly visible status (not Draft or Archived), <c>Public</c>, not hidden by moderation: the
    /// marketplace search's own rule (<c>MarketplaceListing.IsDiscoverable</c>).
    /// <para>
    /// <b>What decides whether a consumer may tell anybody but the seller.</b> A draft's price is the
    /// seller's business. Defaults to false, so a message written before the field existed fails
    /// closed.
    /// </para>
    /// </summary>
    public bool IsListedPublicly { get; init; }

    /// <summary>
    /// Whether the seller publishes the price. <see cref="PriceAmount"/> is the stored figure either
    /// way; a consumer must never report or compare an undisclosed one. Defaults to false.
    /// </summary>
    public bool IsPriceDisclosed { get; init; }

    /// <summary>The listing's category, as <c>ListingPublished</c> carries it — for saved-search matching.</summary>
    public string? CategoryKey { get; init; }

    public string? CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary><c>FixedPrice</c>, <c>Swap</c> and so on, as <c>ListingPublished</c> names them.</summary>
    public string? ListingType { get; init; }

    /// <summary>The item's condition, as <c>ListingPublished</c> names it.</summary>
    public string? Condition { get; init; }
}
