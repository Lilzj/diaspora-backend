namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// The counters a card renders.
/// <para>
/// <b>Views and saves are public; offers and reservations are the seller's.</b> "Four people are
/// watching this" is useful to a buyer and harmless. "Three offers on the table" is a negotiating
/// position, and publishing it would tell every buyer exactly how hard to push — so those two are
/// present only on the seller's own view.
/// </para>
/// </summary>
public sealed record ListingEngagementResponse
{
    public required long Views { get; init; }

    public required long UniqueViews { get; init; }

    public required long Saves { get; init; }

    public required long Shares { get; init; }

    /// <summary>Negotiations opened. Seller's view only.</summary>
    public long? Offers { get; init; }

    /// <summary>Reservations taken. Seller's view only.</summary>
    public long? Reservations { get; init; }
}
