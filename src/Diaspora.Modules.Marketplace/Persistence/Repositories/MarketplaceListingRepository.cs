using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Persistence.Querying;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>
/// EF Core repository for listings.
/// <para>
/// <b>Three read shapes, and the graphs really do differ.</b> <see cref="GetAsync"/> loads
/// everything because a command needs an aggregate that can enforce its own invariants — a
/// partially loaded listing cannot validate an attribute replacement against attributes it never
/// loaded. <see cref="GetForReadAsync"/> loads the same graph untracked, because the detail screen
/// renders all of it. <see cref="QueryAsync"/> loads the <em>card</em> graph: tags and media, and
/// nothing else. A results page of twenty cards that eagerly loaded twenty full listings would be
/// several thousand rows to render twenty titles.
/// </para>
/// <para>
/// <b>Owned collections must be explicitly included.</b> EF loads owned references eagerly and
/// owned collections only when asked — exactly the trap that makes a card query fast and a command
/// silently operate on a listing with no attributes in it.
/// </para>
/// <para>
/// <b>Visibility is applied here, from the filter's audience fields, and nowhere else.</b> Drafts
/// and private listings appear only to their own seller and to staff; hidden ones only to staff.
/// Soft-deleted rows never appear at all — the shared context's global filter owns that predicate.
/// </para>
/// </summary>
public sealed class MarketplaceListingRepository : IMarketplaceListingRepository
{
    private readonly DiasporaDbContext _dbContext;

    public MarketplaceListingRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<MarketplaceListing> Listings => _dbContext.Set<MarketplaceListing>();

    public async Task<MarketplaceListing?> GetAsync(
        MarketplaceListingId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Listings)
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);

    public async Task<MarketplaceListing?> GetForReadAsync(
        MarketplaceListingId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Listings.AsNoTracking())
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);

    public async Task<MarketplaceListing?> GetIncludingDeletedAsync(
        MarketplaceListingId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Listings.IgnoreQueryFilters())
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);

    public async Task<MarketplaceListing?> GetBySlugAsync(
        ListingSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await FullGraph(Listings.AsNoTracking())
            .FirstOrDefaultAsync(listing => listing.Slug.Value == value, cancellationToken);
    }

    public async Task<IReadOnlyList<MarketplaceListing>> GetManyAsync(
        IReadOnlyCollection<MarketplaceListingId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        // Compare whole identifiers, not their inner Guids. `listing.Id.Value` reaches through the
        // strongly-typed id to a member that has no column of its own — the converter maps the id
        // as a unit — so EF cannot translate it and throws at query time rather than compile time.
        var keys = ids.ToList();

        return await CardGraph(Listings.AsNoTracking())
            .Where(listing => keys.Contains(listing.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<MarketplaceListing?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default) =>
        await FullGraph(Listings)
            .FirstOrDefaultAsync(
                listing => listing.TrustMark.VerificationId == verificationId, cancellationToken);

    public async Task<CursorPagedResult<MarketplaceListing>> QueryAsync(
        ListingQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = CardGraph(Apply(Listings.AsNoTracking(), filter));

        query = ApplyOrderAndCursor(query, filter, MarketplaceCursor.Parse(cursor));

        var effectiveLimit = Math.Clamp(limit, 1, MarketplaceLimits.MaxPageSize);

        // One more than asked for, so "is there a next page" is answered by the same query rather
        // than by a second count over the same predicate.
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0 ? EncodeCursor(rows[^1], filter.Sort) : null;

        return CursorPagedResult<MarketplaceListing>.Create(rows, next, hasMore);
    }

    public async Task<int> CountAsync(
        ListingQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Apply(Listings.AsNoTracking(), filter).CountAsync(cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        ListingSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        // Ignores the soft-delete filter: a deleted listing still holds its slug, and handing it to
        // a new listing would break every link ever shared to the old one.
        return await Listings.IgnoreQueryFilters()
            .AnyAsync(listing => listing.Slug.Value == value, cancellationToken);
    }

    public async Task<IReadOnlyList<MarketplaceListing>> GetExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default)
    {
        var live = new[]
        {
            ListingStatus.Published,
            ListingStatus.Active,
            ListingStatus.Reserved
        };

        return await Listings
            .Where(listing =>
                listing.ExpiresAt != null
                && listing.ExpiresAt <= asOf
                && live.Contains(listing.Status))
            .OrderBy(listing => listing.ExpiresAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketplaceListing>> GetPublishedSinceAsync(
        DateTimeOffset since, int limit, CancellationToken cancellationToken = default) =>
        await CardGraph(Listings.AsNoTracking())
            .Where(listing =>
                listing.PublishedAt != null
                && listing.PublishedAt > since
                && listing.Visibility == ListingVisibility.Public
                && (listing.ModerationState == ListingModerationState.Clear
                    || listing.ModerationState == ListingModerationState.Flagged)
                && (listing.Status == ListingStatus.Active
                    || listing.Status == ListingStatus.Published))
            .OrderBy(listing => listing.PublishedAt)
            .Take(Math.Clamp(limit, 1, 1_000))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MarketplaceListing>> GetActiveBySellerAsync(
        SellerProfileId sellerId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sellerId);

        var live = new[]
        {
            ListingStatus.Published,
            ListingStatus.Active,
            ListingStatus.Reserved
        };

        return await Listings
            .Where(listing => listing.SellerId == sellerId && live.Contains(listing.Status))
            .OrderByDescending(listing => listing.PublishedAt)
            .Take(Math.Clamp(limit, 1, 1_000))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountActiveBySellerAsync(
        SellerProfileId sellerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sellerId);

        var live = new[]
        {
            ListingStatus.Draft,
            ListingStatus.Published,
            ListingStatus.Active,
            ListingStatus.Reserved
        };

        // Drafts count. It is drafts a script can create for free that the abuse bound exists to
        // stop, not published listings a person wrote.
        return await Listings.CountAsync(
            listing => listing.SellerId == sellerId && live.Contains(listing.Status),
            cancellationToken);
    }

    /// <summary>
    /// Whether the seller already has a live listing with this exact title.
    /// <para>
    /// Exact rather than fuzzy, deliberately. Near-duplicate detection is a fraud provider's job
    /// behind <c>IListingFraudSignalProvider</c>, where a false positive is a moderator's note; a
    /// fuzzy match here would be a refusal, and refusing a member's second listing of two genuinely
    /// similar items is worse than letting a duplicate through for review.
    /// </para>
    /// </summary>
    public async Task<bool> HasDuplicateAsync(
        SellerProfileId sellerId,
        string title,
        MarketplaceListingId? excluding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sellerId);

        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalized = ListingTitle.Normalize(title);

        if (normalized is null)
        {
            return false;
        }

        var query = Listings.AsNoTracking()
            .Where(listing =>
                listing.SellerId == sellerId
                && listing.Title.Value == normalized
                && listing.Status != ListingStatus.Archived
                && listing.Status != ListingStatus.Sold);

        if (excluding is not null)
        {
            query = query.Where(listing => listing.Id != excluding);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<int> CountInAreaAsync(
        string areaPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(areaPath))
        {
            return 0;
        }

        return await Listings.AsNoTracking()
            .Where(listing =>
                listing.Location.AreaPath != null
                && listing.Location.AreaPath.StartsWith(areaPath)
                && listing.Visibility == ListingVisibility.Public
                && listing.ModerationState == ListingModerationState.Clear
                && (listing.Status == ListingStatus.Active
                    || listing.Status == ListingStatus.Published))
            .CountAsync(cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = ListingLocationContext.NormalizeCountry(countryCode);

        if (country is null)
        {
            return 0;
        }

        return await Listings.AsNoTracking()
            .Where(listing =>
                listing.Location.CountryCode == country
                && listing.Visibility == ListingVisibility.Public
                && listing.ModerationState == ListingModerationState.Clear
                && (listing.Status == ListingStatus.Active
                    || listing.Status == ListingStatus.Published))
            .CountAsync(cancellationToken);
    }

    public async Task AddAsync(
        MarketplaceListing listing, CancellationToken cancellationToken = default) =>
        await Listings.AddAsync(listing, cancellationToken);

    public void Remove(MarketplaceListing listing) => Listings.Remove(listing);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Graphs
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Everything. For a command that must enforce the aggregate's own invariants.</summary>
    private static IQueryable<MarketplaceListing> FullGraph(IQueryable<MarketplaceListing> query) =>
        query
            .Include(listing => listing.Attributes)
            .Include(listing => listing.Tags)
            .Include(listing => listing.MediaReferences)
            .Include(listing => listing.ProviderReferences)
            .Include(listing => listing.GuideReferences)
            .Include(listing => listing.ModerationNotes);

    /// <summary>
    /// What a card renders: the cover image and the chips. Deliberately excludes the attributes,
    /// the references and the moderation trail.
    /// </summary>
    private static IQueryable<MarketplaceListing> CardGraph(IQueryable<MarketplaceListing> query) =>
        query
            .Include(listing => listing.Tags)
            .Include(listing => listing.MediaReferences);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Filtering
    // ════════════════════════════════════════════════════════════════════════════════════════

    private IQueryable<MarketplaceListing> Apply(
        IQueryable<MarketplaceListing> query, ListingQueryFilter filter)
    {
        query = ApplyAudience(query, filter);

        if (filter.ListingIds is { Count: > 0 })
        {
            var ids = filter.ListingIds.ToList();
            query = query.Where(listing => ids.Contains(listing.Id));
        }

        if (filter.SellerId is { } sellerId)
        {
            query = query.Where(listing => listing.SellerId == sellerId);
        }

        if (filter.ProviderId is { } providerId)
        {
            // Correlated against the seller rather than denormalised onto the listing: a shop that
            // unlinks its storefront should stop being a storefront everywhere at once.
            var storefronts = _dbContext.Set<SellerProfile>()
                .Where(seller => seller.ProviderId == providerId)
                .Select(seller => seller.Id);

            query = query.Where(listing => storefronts.Contains(listing.SellerId));
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(listing => statuses.Contains(listing.Status));
        }

        if (filter.AvailableOnly)
        {
            query = query.Where(listing =>
                listing.Status == ListingStatus.Active
                && listing.Inventory.Quantity - listing.Inventory.Reserved
                    - listing.Inventory.Sold > 0);
        }

        query = ApplyClassification(query, filter);
        query = ApplyGeography(query, filter);
        query = ApplyCommerce(query, filter);
        query = ApplyTrustAndModeration(query, filter);

        if (filter.PublishedAfter is { } publishedAfter)
        {
            query = query.Where(listing => listing.PublishedAt > publishedAfter);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            // A LIKE-style match over the write model. Honest and adequate until a real engine
            // arrives; IMarketplaceSearchEngine is the seam that replaces it without a domain
            // change.
            var term = filter.Text.Trim();

            query = query.Where(listing =>
                EF.Functions.Like(listing.Title.Value, $"%{term}%")
                || EF.Functions.Like(listing.Description.Summary, $"%{term}%")
                || (listing.Product.Brand != null
                    && EF.Functions.Like(listing.Product.Brand, $"%{term}%"))
                || (listing.Product.Model != null
                    && EF.Functions.Like(listing.Product.Model, $"%{term}%")));
        }

        return query;
    }

    /// <summary>
    /// The one place drafts, unlisted and hidden listings are let through. Every listing query in
    /// the module goes through it, so the rule is written once rather than remembered per caller.
    /// </summary>
    private static IQueryable<MarketplaceListing> ApplyAudience(
        IQueryable<MarketplaceListing> query, ListingQueryFilter filter)
    {
        if (filter.ViewerIsStaff)
        {
            return query;
        }

        if (filter.IncludeNonPublic && filter.ViewerSellerId is { } viewerSellerId)
        {
            // The seller's own dashboard: everything of theirs, nothing of anyone else's that is
            // not already public.
            return query.Where(listing =>
                listing.SellerId == viewerSellerId
                || (listing.Visibility == ListingVisibility.Public
                    && (listing.ModerationState == ListingModerationState.Clear
                        || listing.ModerationState == ListingModerationState.Flagged)
                    && listing.Status != ListingStatus.Draft
                    && listing.Status != ListingStatus.Archived));
        }

        return query.Where(listing =>
            listing.Visibility == ListingVisibility.Public
            && (listing.ModerationState == ListingModerationState.Clear
                || listing.ModerationState == ListingModerationState.Flagged)
            && listing.Status != ListingStatus.Draft
            && listing.Status != ListingStatus.Archived);
    }

    /// <summary>
    /// Category, tag, type, condition, brand and structured attributes.
    /// <para>
    /// An instance method rather than a static one, because the subtree filter correlates against
    /// the category table: the listing stores only its own key, and copying the materialised path
    /// onto every listing would make a category rename a bulk update over the whole marketplace.
    /// </para>
    /// </summary>
    private IQueryable<MarketplaceListing> ApplyClassification(
        IQueryable<MarketplaceListing> query, ListingQueryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.CategoryKey))
        {
            var category = filter.CategoryKey.Trim().ToLowerInvariant();
            query = query.Where(listing => listing.CategoryKey == category);
        }

        if (!string.IsNullOrWhiteSpace(filter.CategoryPathPrefix))
        {
            // A subtree. Correlated against the category table's materialised path, because the
            // listing stores only its own key — copying the path onto every listing would make a
            // category rename a bulk update over the whole marketplace.
            var prefix = filter.CategoryPathPrefix;

            var keys = _dbContext.Set<Domain.Catalog.ListingCategory>()
                .Where(category => category.Path.StartsWith(prefix))
                .Select(category => category.Key);

            query = query.Where(listing => keys.Contains(listing.CategoryKey));
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tag = filter.Tag.Trim().ToLowerInvariant();
            query = query.Where(listing => listing.Tags.Any(assignment => assignment.Slug == tag));
        }

        if (filter.ListingTypes is { Count: > 0 })
        {
            var types = filter.ListingTypes.ToList();
            query = query.Where(listing => types.Contains(listing.ListingType));
        }

        if (filter.MinimumCondition is { } condition)
        {
            // Conditions are ordered worst-to-best, so "at least Good" is a range rather than a
            // set membership test over a hand-maintained list — but the range is taken over the enum
            // and sent as the names it admits. The column stores the name, so `>=` in SQL compared
            // words: "at least Excellent" returned a phone in Good condition, because G follows E,
            // and "at least Good" admitted NotApplicable. See StoredEnum.
            var admitted = StoredEnum.AtLeast(condition);
            query = query.Where(listing => admitted.Contains(listing.Condition));
        }

        if (!string.IsNullOrWhiteSpace(filter.Brand))
        {
            var brand = filter.Brand.Trim();
            query = query.Where(listing => listing.Product.Brand == brand);
        }

        if (!string.IsNullOrWhiteSpace(filter.Model))
        {
            var model = filter.Model.Trim();
            query = query.Where(listing => listing.Product.Model == model);
        }

        if (filter.Attributes is { Count: > 0 })
        {
            // Every pair must match, unlike tags where any match qualifies: somebody asking for a
            // 128GB black phone means both. One EXISTS per pair, which the composite index on
            // (key, value) serves.
            foreach (var (key, value) in filter.Attributes)
            {
                var attributeKey = key;
                var attributeValue = value;

                query = query.Where(listing => listing.Attributes.Any(attribute =>
                    attribute.Key == attributeKey && attribute.Value == attributeValue));
            }
        }

        return query;
    }

    private static IQueryable<MarketplaceListing> ApplyGeography(
        IQueryable<MarketplaceListing> query, ListingQueryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(listing => listing.Location.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            var city = filter.CityName.Trim();
            query = query.Where(listing => listing.Location.CityName == city);
        }

        if (!string.IsNullOrWhiteSpace(filter.AreaPathPrefix))
        {
            // A plain string prefix match — B4's EF lesson. The materialised path is stored
            // precisely so this is a LIKE 'prefix%' against an indexed column.
            //
            // A listing that ships is deliberately never excluded by an area narrowing: hiding
            // everything a buyer in Peckham could have posted to them answers a question nobody
            // asked.
            var prefix = filter.AreaPathPrefix;

            if (filter.AreaPathAncestors is { Count: > 0 })
            {
                var ancestors = filter.AreaPathAncestors.ToList();

                query = query.Where(listing =>
                    (listing.Location.AreaPath != null
                        && (listing.Location.AreaPath.StartsWith(prefix)
                            || ancestors.Contains(listing.Location.AreaPath)))
                    || listing.Fulfillment.OffersShipping);
            }
            else
            {
                query = query.Where(listing =>
                    (listing.Location.AreaPath != null
                        && listing.Location.AreaPath.StartsWith(prefix))
                    || listing.Fulfillment.OffersShipping);
            }
        }

        return query;
    }

    private static IQueryable<MarketplaceListing> ApplyCommerce(
        IQueryable<MarketplaceListing> query, ListingQueryFilter filter)
    {
        if (filter.FreeOnly)
        {
            query = query.Where(listing =>
                listing.Price.IsDisclosed && listing.Price.Amount == 0m);
        }

        if (filter.NegotiableOnly)
        {
            query = query.Where(listing => listing.Price.IsNegotiable);
        }

        if (filter.OffersDeliveryOnly)
        {
            query = query.Where(listing => listing.Fulfillment.OffersDelivery);
        }

        if (filter.OffersShippingOnly)
        {
            query = query.Where(listing => listing.Fulfillment.OffersShipping);
        }

        if (filter.PickupOnly)
        {
            query = query.Where(listing =>
                !listing.Fulfillment.OffersDelivery && !listing.Fulfillment.OffersShipping);
        }

        // Applied only to listings quoting the same currency, and that is deliberate. Nothing in
        // this module converts — a conversion needs a rate, a rate has a date, and a price silently
        // restated at yesterday's rate is a number nobody can act on. A filter that matches nothing
        // is better than one that matches wrongly.
        if (string.IsNullOrWhiteSpace(filter.Currency))
        {
            return query;
        }

        if (filter.MinimumPrice is null && filter.MaximumPrice is null)
        {
            return query;
        }

        var currency = filter.Currency.Trim().ToUpperInvariant();

        query = query.Where(listing =>
            listing.Price.IsDisclosed && listing.Price.CurrencyCode == currency);

        if (filter.MinimumPrice is { } minimum)
        {
            query = query.Where(listing => listing.Price.Amount >= minimum);
        }

        if (filter.MaximumPrice is { } maximum)
        {
            query = query.Where(listing => listing.Price.Amount <= maximum);
        }

        return query;
    }

    private IQueryable<MarketplaceListing> ApplyTrustAndModeration(
        IQueryable<MarketplaceListing> query, ListingQueryFilter filter)
    {
        if (filter.FeaturedOnly)
        {
            query = query.Where(listing => listing.IsFeatured);
        }

        // Correlated against the seller rather than a denormalised flag on the listing. A badge
        // copied onto the listing at publication would keep saying "verified" after Trust revoked
        // it — which is the platform vouching for exactly the wrong party, and is the single worst
        // thing this filter could get wrong.
        if (filter.VerifiedSellersOnly)
        {
            var verified = _dbContext.Set<SellerProfile>()
                .Where(seller =>
                    seller.TrustMark.State == ListingVerificationState.Verified
                    && seller.Status == SellerStatus.Active)
                .Select(seller => seller.Id);

            query = query.Where(listing => verified.Contains(listing.SellerId));
        }

        if (filter.SellerKinds is { Count: > 0 })
        {
            var kinds = filter.SellerKinds.ToList();

            var matching = _dbContext.Set<SellerProfile>()
                .Where(seller => kinds.Contains(seller.Identity.Kind))
                .Select(seller => seller.Id);

            query = query.Where(listing => matching.Contains(listing.SellerId));
        }

        // The moderation queue's own filters. The audience predicate has already refused these to
        // anybody but staff, so applying them for a member narrows an already-empty set.
        if (filter.ReportedOnly)
        {
            query = query.Where(listing => listing.ReportCount > 0);
        }

        if (filter.ModerationState is { } moderationState)
        {
            query = query.Where(listing => listing.ModerationState == moderationState);
        }

        return query;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Ordering & cursors
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Orders the query and applies the continuation.
    /// <para>
    /// Every ordering ends with the slug, which is the tie-break: unique, indexed and a string,
    /// because the primary key is a value-converted identifier EF cannot compare in SQL. Without a
    /// stable tie-break, two listings published in the same millisecond can swap places between
    /// pages and one of them is never shown — which on a marketplace is an item somebody never
    /// bought.
    /// </para>
    /// <para>
    /// <b>CA1309 is suppressed across the whole method, and it has to be.</b> The analyser wants
    /// <c>string.Compare(a, b, StringComparison.Ordinal)</c>, and EF cannot translate that overload
    /// — the query would fall back to client evaluation, which for a cursor predicate means loading
    /// the table to find page two. The two-argument overload translates to the database's own
    /// collation, which is what actually orders the rows, so comparing any other way would in fact
    /// be the bug. B8's and B9's repositories carry the same suppression for the same reason.
    /// </para>
    /// </summary>
#pragma warning disable CA1309
    private static IQueryable<MarketplaceListing> ApplyOrderAndCursor(
        IQueryable<MarketplaceListing> query,
        ListingQueryFilter filter,
        MarketplaceCursor? cursor)
    {
        switch (filter.Sort)
        {
            case ListingSortOrder.PriceLowToHigh:
            {
                if (cursor?.AsNumber() is { } value)
                {
                    query = query.Where(listing =>
                        listing.Price.Amount > (decimal)value
                        || (listing.Price.Amount == (decimal)value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderBy(listing => listing.Price.Amount)
                    .ThenBy(listing => listing.Slug.Value);
            }

            case ListingSortOrder.PriceHighToLow:
            {
                if (cursor?.AsNumber() is { } value)
                {
                    query = query.Where(listing =>
                        listing.Price.Amount < (decimal)value
                        || (listing.Price.Amount == (decimal)value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(listing => listing.Price.Amount)
                    .ThenBy(listing => listing.Slug.Value);
            }

            case ListingSortOrder.MostViewed:
            {
                if (cursor?.AsNumber() is { } value)
                {
                    query = query.Where(listing =>
                        listing.Engagement.ViewCount < (long)value
                        || (listing.Engagement.ViewCount == (long)value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(listing => listing.Engagement.ViewCount)
                    .ThenBy(listing => listing.Slug.Value);
            }

            case ListingSortOrder.EndingSoon:
            {
                if (cursor?.AsTimestamp() is { } value)
                {
                    query = query.Where(listing =>
                        listing.ExpiresAt > value
                        || (listing.ExpiresAt == value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                // Listings with no expiry sort last: "ending soon" is meaningless for one that
                // never ends, and putting them first would bury the urgent ones.
                return query
                    .OrderBy(listing => listing.ExpiresAt == null)
                    .ThenBy(listing => listing.ExpiresAt)
                    .ThenBy(listing => listing.Slug.Value);
            }

            case ListingSortOrder.Featured:
            {
                if (cursor?.AsTimestamp() is { } value)
                {
                    query = query.Where(listing =>
                        listing.PublishedAt < value
                        || (listing.PublishedAt == value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                // Featured first, then newest. A promotion lifts a listing and never buries a
                // better match, which is why this is a secondary key rather than a score.
                return query
                    .OrderByDescending(listing => listing.IsFeatured)
                    .ThenByDescending(listing => listing.PublishedAt)
                    .ThenBy(listing => listing.Slug.Value);
            }

            // Relevance and Nearest both degrade to newest here, and honestly: the shipped EF
            // engine has no score and no geometry. A real engine implements both properly behind
            // IMarketplaceSearchEngine, and nothing above it changes.
            default:
            {
                if (cursor?.AsTimestamp() is { } value)
                {
                    query = query.Where(listing =>
                        listing.PublishedAt < value
                        || (listing.PublishedAt == value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(listing => listing.PublishedAt)
                    .ThenBy(listing => listing.Slug.Value);
            }
        }
    }
#pragma warning restore CA1309

    private static string EncodeCursor(MarketplaceListing last, ListingSortOrder sort) => sort switch
    {
        ListingSortOrder.PriceLowToHigh or ListingSortOrder.PriceHighToLow =>
            MarketplaceCursor.FromNumber(
                (double)(last.Price.Amount ?? 0m), last.Slug.Value).Encode(),
        ListingSortOrder.MostViewed =>
            MarketplaceCursor.FromNumber(
                last.Engagement.ViewCount, last.Slug.Value).Encode(),
        ListingSortOrder.EndingSoon =>
            MarketplaceCursor.FromTimestamp(
                last.ExpiresAt ?? DateTimeOffset.MaxValue, last.Slug.Value).Encode(),
        _ => MarketplaceCursor.FromTimestamp(
            last.PublishedAt ?? last.CreatedAt, last.Slug.Value).Encode()
    };
}
