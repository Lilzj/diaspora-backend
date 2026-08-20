namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Restates how many units a listing has.
/// <para>
/// Its own route rather than a field on the update, because stock is the one thing a seller
/// changes without touching anything else — a shop restocking does not want to re-send a
/// twenty-thousand-character description to do it, and a stock edit that failed the revision check
/// because somebody fixed a typo would be infuriating.
/// </para>
/// </summary>
public sealed record SetListingStockRequest
{
    public required int Quantity { get; init; }

    /// <summary>
    /// Whether the platform counts stock down. False on a service or a wanted advert, where "one
    /// of" is not a meaningful statement.
    /// </summary>
    public bool IsTracked { get; init; } = true;

    public int? BaseRevision { get; init; }
}
