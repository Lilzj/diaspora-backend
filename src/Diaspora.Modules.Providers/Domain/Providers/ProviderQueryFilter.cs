using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// Everything a discovery query can ask the provider table for.
/// <para>
/// <b>This type is why ranking strategies and search projection contributors are safe to
/// contribute.</b> A strategy composes a filter and hands it back; it never composes an
/// expression tree, never touches a <c>DbContext</c>, and cannot write a predicate EF fails to
/// translate at run time inside another module's stack trace. Every field here is one the
/// repository knows how to translate, which is a promise the repository can actually keep — and
/// it is the same discipline B6 applied to feeds after B4 recorded a value-converted member
/// reaching a <c>StartsWith</c> and failing only in production.
/// </para>
/// <para>
/// The bounding box is the one piece of geometry B7 computes for itself, and it is a
/// <b>prefilter</b>: it narrows candidates to a rectangle so Location is asked for real
/// distances about tens of providers rather than tens of thousands. Every distance a member ever
/// sees comes from <c>ILocationDirectory</c>. B7 never rounds, sorts or renders a distance it
/// worked out itself.
/// </para>
/// </summary>
public sealed record ProviderQueryFilter
{
    /// <summary>Which statuses count. Active only, unless a caller says otherwise.</summary>
    public IReadOnlyCollection<ProviderStatus> Statuses { get; init; } = [ProviderStatus.Active];

    /// <summary>Restrict to specific providers. Used by the bookmark and "mine" screens.</summary>
    public IReadOnlyCollection<ProviderId>? ProviderIds { get; init; }

    /// <summary>Free text over name, summary and tags. Naive by design — B15 owns real search.</summary>
    public string? Query { get; init; }

    public IReadOnlyCollection<ProviderType>? ProviderTypes { get; init; }

    /// <summary>An exact category. Combine with <see cref="CategoryPathPrefix"/> for a subtree.</summary>
    public ServiceCategoryId? CategoryId { get; init; }

    /// <summary>
    /// The materialised category path of a subtree root. Prefix-matched, so choosing
    /// "Food &amp; Restaurants" returns everything beneath it without B7 walking the tree.
    /// </summary>
    public string? CategoryPathPrefix { get; init; }

    /// <summary>Tag slugs. A provider matching any of them qualifies.</summary>
    public IReadOnlyCollection<string>? TagSlugs { get; init; }

    /// <summary>BCP-47 codes. A provider serving any of them qualifies.</summary>
    public IReadOnlyCollection<string>? LanguageCodes { get; init; }

    public IReadOnlyCollection<ServiceDeliveryMode>? DeliveryModes { get; init; }

    /// <summary>The client's "Show only Community Verified providers" toggle.</summary>
    public bool VerifiedOnly { get; init; }

    /// <summary>The client's featured carousel.</summary>
    public bool FeaturedOnly { get; init; }

    /// <summary>Only providers holding at least one current certification.</summary>
    public bool CertifiedOnly { get; init; }

    /// <summary>Minimum average rating, 1–5.</summary>
    public double? MinimumRating { get; init; }

    /// <summary>Minimum number of reviews, so one five-star review does not top a hundred.</summary>
    public int? MinimumReviewCount { get; init; }

    // ── Geography, all resolved by B4 before reaching here ──────────────────────────────────

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    /// <summary>Prefix-matched against the copied hierarchy path. A subtree of geography.</summary>
    public string? HierarchyPathPrefix { get; init; }

    /// <summary>
    /// Every ancestor path of <see cref="HierarchyPathPrefix"/>, itself included.
    /// <para>
    /// <b>This exists because containment runs in two directions and only one of them is a
    /// prefix match a database can index.</b> "Is this provider inside London" compares the
    /// provider's own path against the requested prefix — a <c>LIKE 'prefix%'</c>, indexed and
    /// fast. "Does this provider's declared service area <em>cover</em> Peckham" is the reverse:
    /// the area's path (<c>/gb/london/</c>) is an ancestor of the request's
    /// (<c>/gb/london/southwark/peckham/</c>), which as an expression is
    /// <c>request.StartsWith(column)</c> — a shape EF Core does not translate and that would
    /// throw at run time inside a query nobody could see was wrong from reading it.
    /// </para>
    /// <para>
    /// Enumerating the ancestors turns it into an <c>IN</c> over an indexed column. The list is
    /// short — the catalogue is at most five levels deep — and it is computed once by the caller
    /// that already resolved the geography.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<string>? AncestorHierarchyPaths { get; init; }

    public string? CityName { get; init; }

    /// <summary>The prefilter rectangle. Never the answer — only a way to narrow the candidates.</summary>
    public double? MinLatitude { get; init; }

    public double? MaxLatitude { get; init; }

    public double? MinLongitude { get; init; }

    public double? MaxLongitude { get; init; }

    /// <summary>
    /// Whether to include providers that serve remotely regardless of the geographic scope.
    /// <para>
    /// Default true, and the default is the interesting part: a remote immigration adviser
    /// excluded from a radius search is a bug rather than a filter working correctly, and a
    /// model where the caller has to remember to opt in gets it wrong most of the time.
    /// </para>
    /// </summary>
    public bool IncludeRemoteProviders { get; init; } = true;

    /// <summary>
    /// Whether to match a provider's declared service areas as well as its own address.
    /// What makes a mobile hairdresser findable outside her own street.
    /// </summary>
    public bool MatchServiceAreas { get; init; } = true;

    // ── Audience ────────────────────────────────────────────────────────────────────────────

    /// <summary>The audiences a viewer is eligible for. Computed before the query, never in it.</summary>
    public IReadOnlyCollection<ProviderVisibilityLevel>? AllowedVisibility { get; init; }

    /// <summary>The viewer, for including their own unpublished providers on the "mine" screen.</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>Restricts to providers the viewer holds a role on. The "My Listings" query.</summary>
    public bool OwnedByViewerOnly { get; init; }

    /// <summary>Excludes providers owned by these members. How blocking reaches discovery.</summary>
    public IReadOnlyCollection<Guid>? ExcludedOwnerIds { get; init; }

    // ── Ordering and paging ─────────────────────────────────────────────────────────────────

    public ProviderSortOrder Sort { get; init; } = ProviderSortOrder.Recommended;

    /// <summary>An opaque continuation token. Cursor rather than offset, for infinite scroll.</summary>
    public string? Cursor { get; init; }

    public int Limit { get; init; } = ProviderLimits.DefaultPageSize;

    /// <summary>Whether the caller needs a total. Off by default — counting is the expensive half.</summary>
    public bool IncludeTotalCount { get; init; }

    /// <summary>Whether the geographic scope is spatial enough to sort by distance.</summary>
    public bool HasBoundingBox =>
        MinLatitude is not null && MaxLatitude is not null
        && MinLongitude is not null && MaxLongitude is not null;

    /// <summary>
    /// Whether the rectangle wraps across ±180°, which it does exactly when its western edge is
    /// numerically east of its eastern edge.
    /// <para>
    /// <b>Not a cosmetic flag.</b> Without it the predicate is
    /// <c>longitude &gt;= 179 AND longitude &lt;= -179</c>, which no row on the planet satisfies:
    /// a member in Suva or Tarawa panning across the date line gets an empty map with no error
    /// and nothing in the logs. B4 supplies the box and B4 knows about the wrap — this is B7
    /// carrying it through rather than flattening it into two inequalities that cannot express it.
    /// </para>
    /// </summary>
    public bool BoundingBoxCrossesAntimeridian =>
        HasBoundingBox && MinLongitude > MaxLongitude;

    /// <summary>The limit, clamped to what one page may return.</summary>
    public int EffectiveLimit => Math.Clamp(Limit, 1, ProviderLimits.MaxPageSize);
}
