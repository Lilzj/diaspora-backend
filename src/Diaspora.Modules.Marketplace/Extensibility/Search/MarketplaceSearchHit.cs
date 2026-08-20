namespace Diaspora.Modules.Marketplace.Extensibility.Search;

/// <summary>
/// One result: an identifier and a score.
/// <para>
/// <b>Deliberately not a document.</b> The read path hydrates the card from the write model, so a
/// stale index can misrank a result but can never misrender one and can never leak a listing whose
/// price, stock or visibility changed after it was indexed.
/// </para>
/// </summary>
public sealed record MarketplaceSearchHit
{
    public required Guid ListingId { get; init; }

    /// <summary>
    /// The engine's own relevance number. Meaningful only within one result set — comparing
    /// scores across two searches, or across two engines, is meaningless.
    /// </summary>
    public double Score { get; init; }
}
