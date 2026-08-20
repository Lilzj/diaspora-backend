using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Analytics;

/// <summary>
/// The measured record of one listing.
/// <para>
/// <b>Separate from the listing's own engagement counters, and the split is about read cost.</b>
/// The counters on the listing are read on every card in every result set, so they live on the row
/// the card already loaded. This is read once, by a seller looking at their dashboard, and it
/// carries the things a dashboard needs and a card never does: a daily series, a funnel, and the
/// elapsed times the brief asks for.
/// </para>
/// <para>
/// <b>Not soft-deletable, deliberately.</b> A rollup outlives the listing's visibility, because a
/// seller's own history — how long their things take to sell, how often an offer converts — is
/// computed from listings they have long since archived. Making it soft-deletable would make a
/// seller's dashboard forget everything they ever sold.
/// </para>
/// <para>
/// <b>Created eagerly with the listing, never lazily on first view.</b> "Create it if it is
/// missing" is a race that two concurrent views will lose, and the row it produces twice violates
/// the one-per-listing key.
/// </para>
/// </summary>
public sealed class ListingAnalytics : AggregateRoot<ListingAnalyticsId>, IAuditableEntity
{
    private readonly List<ListingDailyMetric> _daily = [];

    private ListingAnalytics(
        ListingAnalyticsId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        string categoryKey,
        string countryCode,
        DateTimeOffset createdAt)
        : base(id)
    {
        ListingId = listingId;
        SellerId = sellerId;
        CategoryKey = categoryKey;
        CountryCode = countryCode;
        CreatedAt = createdAt;
    }

    private ListingAnalytics() { }

    public MarketplaceListingId ListingId { get; private set; } = default!;

    public SellerProfileId SellerId { get; private set; } = default!;

    /// <summary>
    /// Denormalised so category and geographic trends are a group-by on this table rather than a
    /// join to every listing in the marketplace.
    /// </summary>
    public string CategoryKey { get; private set; } = string.Empty;

    /// <summary>Denormalised for the same reason. The brief's geographic trends read this.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    // ── Lifetime totals ─────────────────────────────────────────────────────────────────────

    public long TotalViews { get; private set; }

    public long TotalUniqueViews { get; private set; }

    public long TotalSaves { get; private set; }

    public long TotalShares { get; private set; }

    public long TotalOffers { get; private set; }

    public long TotalReservations { get; private set; }

    public long TotalSales { get; private set; }

    public long TotalImpressions { get; private set; }

    // ── Elapsed times ───────────────────────────────────────────────────────────────────────

    public DateTimeOffset? FirstPublishedAt { get; private set; }

    public DateTimeOffset? FirstOfferAt { get; private set; }

    public DateTimeOffset? FirstSaleAt { get; private set; }

    /// <summary>
    /// The brief's time to sale, computed once and stored. Recomputing it from two timestamps on
    /// every dashboard read is cheap; storing it makes "median time to sale by category" a single
    /// aggregate rather than a projection over the whole table.
    /// </summary>
    public TimeSpan? TimeToFirstSale { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Offers per unique viewer. The brief's offer rate.</summary>
    public double? OfferRate =>
        TotalUniqueViews == 0 ? null : (double)TotalOffers / TotalUniqueViews;

    /// <summary>Sales per unique viewer. The brief's conversion rate.</summary>
    public double? ConversionRate =>
        TotalUniqueViews == 0 ? null : (double)TotalSales / TotalUniqueViews;

    /// <summary>Reservations per unique viewer. The brief's reservation rate.</summary>
    public double? ReservationRate =>
        TotalUniqueViews == 0 ? null : (double)TotalReservations / TotalUniqueViews;

    /// <summary>Views per result-set appearance. How well the card earns its slot.</summary>
    public double? ClickThroughRate =>
        TotalImpressions == 0 ? null : (double)TotalViews / TotalImpressions;

    /// <summary>The daily series, oldest first.</summary>
    public IReadOnlyList<ListingDailyMetric> Daily =>
        _daily.OrderBy(entry => entry.Day).ToList().AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static ListingAnalytics Create(
        ListingAnalyticsId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        string? categoryKey,
        string? countryCode,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(sellerId);

        return new ListingAnalytics(
            id, listingId, sellerId, categoryKey ?? string.Empty, countryCode ?? string.Empty, now);
    }

    /// <summary>Keeps the denormalised dimensions honest when the listing moves.</summary>
    public void Reclassify(string? categoryKey, string? countryCode)
    {
        if (!string.IsNullOrWhiteSpace(categoryKey))
        {
            CategoryKey = categoryKey;
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            CountryCode = countryCode;
        }
    }

    public void RecordPublication(DateTimeOffset at) => FirstPublishedAt ??= at;

    public void RecordView(DateTimeOffset at, bool unique)
    {
        TotalViews++;

        if (unique)
        {
            TotalUniqueViews++;
        }

        Bucket(at).Apply(views: 1, uniqueViews: unique ? 1 : 0);
    }

    public void RecordSaveDelta(DateTimeOffset at, int delta)
    {
        TotalSaves = Math.Max(0, TotalSaves + delta);
        Bucket(at).Apply(saves: delta);
    }

    public void RecordShare(DateTimeOffset at)
    {
        TotalShares++;
        Bucket(at).Apply(shares: 1);
    }

    public void RecordOffer(DateTimeOffset at)
    {
        TotalOffers++;
        FirstOfferAt ??= at;
        Bucket(at).Apply(offers: 1);
    }

    public void RecordReservation(DateTimeOffset at)
    {
        TotalReservations++;
        Bucket(at).Apply(reservations: 1);
    }

    public void RecordSale(DateTimeOffset at, int quantity)
    {
        var units = Math.Max(1, quantity);

        TotalSales += units;

        if (FirstSaleAt is null)
        {
            FirstSaleAt = at;
            TimeToFirstSale = FirstPublishedAt is { } published ? at - published : null;
        }

        Bucket(at).Apply(sales: units);
    }

    public void RecordImpressions(DateTimeOffset at, int count)
    {
        var impressions = Math.Max(0, count);

        TotalImpressions += impressions;
        Bucket(at).Apply(impressions: impressions);
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    /// <summary>
    /// The bucket for a day, created if this is the first movement in it. Bounded by nothing here
    /// — a listing that lives for three years has a thousand rows, which is the point of the
    /// daily grain — and pruned by a retention job outside the aggregate.
    /// </summary>
    private ListingDailyMetric Bucket(DateTimeOffset at)
    {
        var day = DateOnly.FromDateTime(at.UtcDateTime);
        var existing = _daily.Find(entry => entry.Day == day);

        if (existing is not null)
        {
            return existing;
        }

        var created = ListingDailyMetric.For(day);
        _daily.Add(created);

        return created;
    }
}
