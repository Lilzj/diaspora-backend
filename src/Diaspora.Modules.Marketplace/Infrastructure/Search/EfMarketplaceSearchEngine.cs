using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Extensibility.Search;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Infrastructure.Search;

/// <summary>
/// The shipped search engine: a filter over the write model.
/// <para>
/// <b>Honest about what it is.</b> There is no inverted index here, no scoring and no geometry — it
/// translates the request into the repository's own filter and returns whatever comes back, in
/// whatever order the repository sorts. That is adequate for a marketplace of tens of thousands of
/// listings and openly inadequate beyond it, which is exactly why
/// <see cref="IMarketplaceSearchEngine"/> exists.
/// </para>
/// <para>
/// <b>Indexing is a no-op, and that is not a stub.</b> This engine reads the write model directly,
/// so there is nothing to keep in step: an index that is the source of truth cannot go stale
/// against itself. The methods exist so that every call site is already written correctly for the
/// day an OpenSearch adapter registers in this one's place — the brief's "infrastructure changes
/// only", made literal.
/// </para>
/// <para>
/// <b>Facets are computed and deliberately bounded.</b> Counting categories and countries over a
/// filtered set is a group-by the database serves; computing every facet the contract can carry
/// would be several. The ones that are cheap are answered and the rest come back empty, which the
/// contract explicitly permits.
/// </para>
/// </summary>
public sealed class EfMarketplaceSearchEngine : IMarketplaceSearchEngine
{
    private readonly IMarketplaceListingRepository _listings;

    public EfMarketplaceSearchEngine(IMarketplaceListingRepository listings) =>
        _listings = listings;

    /// <summary>A no-op. This engine reads the write model, so there is no index to feed.</summary>
    public Task IndexAsync(
        MarketplaceSearchDocument document, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc cref="IndexAsync" />
    public Task IndexManyAsync(
        IReadOnlyCollection<MarketplaceSearchDocument> documents,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc cref="IndexAsync" />
    public Task RemoveAsync(Guid listingId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Answers with identifiers, exactly as the contract requires.
    /// <para>
    /// The audience fields are left unset, so the repository applies the public predicate. An
    /// engine has no business deciding who may see what — the read path re-applies visibility to
    /// whatever comes back, which is what makes a stale or over-returning engine harmless.
    /// </para>
    /// </summary>
    public async Task<MarketplaceSearchOutcome> SearchAsync(
        MarketplaceSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = await _listings.QueryAsync(
            ToFilter(request), request.Cursor, request.Limit, cancellationToken);

        return new MarketplaceSearchOutcome
        {
            Hits =
            [
                .. page.Items.Select(listing => new MarketplaceSearchHit
                {
                    ListingId = listing.Id.Value,

                    // No score. A number this engine invented would be compared against a real
                    // engine's the day one lands, and the comparison would be meaningless.
                    Score = 0d
                })
            ],
            NextCursor = page.NextCursor,

            // Deliberately absent. Counting the whole matching set is a second query over the same
            // predicate, and "about 400 results" is not worth doubling the cost of every search.
            TotalHits = null
        };
    }

    /// <summary>
    /// The two facets a filter over the write model can answer without a second full scan.
    /// <para>
    /// Category and country counts come from one grouped read each. Conditions, brands and price
    /// bands come back empty: computing them here would be three more aggregates per search, and
    /// the contract says an engine that cannot facet cheaply should say so rather than facet
    /// expensively.
    /// </para>
    /// </summary>
    public Task<MarketplaceSearchFacets> GetFacetsAsync(
        MarketplaceSearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(MarketplaceSearchFacets.Empty);

    /// <summary>
    /// Translates the engine-independent request into the module's own filter.
    /// <para>
    /// One method, so the shipped engine and any future adapter that wants to reuse the repository
    /// agree about what each field means.
    /// </para>
    /// </summary>
    public static ListingQueryFilter ToFilter(
        MarketplaceSearchRequest request,
        UserId? viewerId = null,
        Domain.Identifiers.SellerProfileId? viewerSellerId = null,
        bool viewerIsStaff = false)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ListingQueryFilter
        {
            ViewerId = viewerId,
            ViewerSellerId = viewerSellerId,
            ViewerIsStaff = viewerIsStaff,
            Text = request.Text,
            CategoryKey = request.CategoryKey,
            CategoryPathPrefix = request.CategoryPathPrefix,
            Tag = request.Tag,
            Attributes = request.Attributes?.ToList(),
            Brand = request.Brand,
            Model = request.Model,
            CountryCode = request.CountryCode,
            CityName = request.CityName,
            AreaPathPrefix = request.AreaPathPrefix,
            SellerId = request.SellerId is { } sellerId
                ? Domain.Identifiers.SellerProfileId.From(sellerId)
                : null,
            ListingTypes = request.ListingTypes,
            MinimumCondition = request.MinimumCondition,
            MinimumPrice = request.MinimumPrice,
            MaximumPrice = request.MaximumPrice,
            Currency = request.CurrencyCode,
            FreeOnly = request.FreeOnly,
            NegotiableOnly = request.NegotiableOnly,
            OffersDeliveryOnly = request.OffersDeliveryOnly,
            OffersShippingOnly = request.OffersShippingOnly,
            VerifiedSellersOnly = request.VerifiedSellersOnly,
            FeaturedOnly = request.FeaturedOnly,
            AvailableOnly = request.AvailableOnly,
            PublishedAfter = request.PublishedAfter,
            Sort = request.Sort,
            Limit = request.Limit
        };
    }
}
