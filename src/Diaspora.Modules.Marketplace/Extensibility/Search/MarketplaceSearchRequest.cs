using Diaspora.Modules.Marketplace.Domain.Enums;

namespace Diaspora.Modules.Marketplace.Extensibility.Search;

/// <summary>
/// The question a search engine is asked.
/// <para>
/// <b>Filters an engine cannot serve it may ignore.</b> The repository re-applies visibility and
/// every hard filter to the identifiers that come back, so an engine that over-returns costs a
/// wasted row rather than leaking a draft. An engine that under-returns is simply a worse engine,
/// which is a quality problem rather than a correctness one.
/// </para>
/// <para>
/// This is the one place in the extensibility surface that names domain enums, and it is a
/// deliberate exception: they are the vocabulary the request is expressed in, an adapter switches
/// on their names, and flattening them to strings here would only move the parsing into every
/// adapter.
/// </para>
/// </summary>
public sealed record MarketplaceSearchRequest
{
    /// <summary>The free-text term. Null browses rather than searches.</summary>
    public string? Text { get; init; }

    public string? CategoryKey { get; init; }

    /// <summary>A category subtree, matched as a path prefix.</summary>
    public string? CategoryPathPrefix { get; init; }

    public string? Tag { get; init; }

    /// <summary>Structured attribute filters. A listing must match every pair.</summary>
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }

    public string? Brand { get; init; }

    public string? Model { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    /// <summary>Location's materialised area path, matched as a prefix.</summary>
    public string? AreaPathPrefix { get; init; }

    public Guid? SellerId { get; init; }

    public IReadOnlyList<ListingType>? ListingTypes { get; init; }

    /// <summary>The worst condition acceptable. Conditions are ordered worst-to-best.</summary>
    public ProductCondition? MinimumCondition { get; init; }

    /// <summary>
    /// The price band, with its currency. An engine must not convert between currencies — a rate
    /// has a date, and a price silently restated is a number a buyer would act on and be wrong
    /// about.
    /// </summary>
    public decimal? MinimumPrice { get; init; }

    public decimal? MaximumPrice { get; init; }

    public string? CurrencyCode { get; init; }

    public bool FreeOnly { get; init; }

    public bool NegotiableOnly { get; init; }

    public bool OffersDeliveryOnly { get; init; }

    public bool OffersShippingOnly { get; init; }

    /// <summary>Neither delivery nor shipping offered: the buyer collects.</summary>
    public bool PickupOnly { get; init; }

    public bool VerifiedSellersOnly { get; init; }

    public bool FeaturedOnly { get; init; }

    /// <summary>Only listings a buyer could commit to. True on nearly every real search.</summary>
    public bool AvailableOnly { get; init; } = true;

    public DateTimeOffset? PublishedAfter { get; init; }

    public ListingSortOrder Sort { get; init; } = ListingSortOrder.Relevance;

    /// <summary>Opaque and engine-owned. From a previous outcome's continuation.</summary>
    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
