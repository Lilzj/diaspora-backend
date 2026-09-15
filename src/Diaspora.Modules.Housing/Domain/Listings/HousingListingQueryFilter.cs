using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Domain.Listings;

/// <summary>
/// Everything a listing query can narrow by, in one record.
/// <para>
/// <b>A record with <c>with</c>-expressions rather than a fluent builder or twenty method
/// overloads.</b> Search, nearby, my-listings, the neighbourhood page, the landlord page, the
/// moderation queue and the resource counter all narrow the same table in different combinations,
/// and a parameter list per caller would mean the audience rule — who may see a draft — was
/// written seven times and correct in six.
/// </para>
/// <para>
/// <b>The audience fields are the ones that matter.</b> <see cref="ViewerId"/>,
/// <see cref="ViewerLandlordId"/>, <see cref="ViewerIsStaff"/> and <see cref="IncludeNonPublic"/>
/// are filled in by the authorisation service and by nothing else; a query that forgets to ask for
/// them gets the public view, which is the safe direction to fail in.
/// </para>
/// <para>
/// <b>Price narrowing is currency-and-period scoped, deliberately.</b> Nothing in this module
/// converts a currency or normalises a rent period — see <c>HousingPrice</c> — so a price range
/// with no currency narrows nothing rather than comparing pounds against rupees.
/// </para>
/// </summary>
public sealed record HousingListingQueryFilter
{
    // ── Audience ────────────────────────────────────────────────────────────────────────────

    /// <summary>Who is asking. Null for a signed-out visitor.</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>The caller's own landlord profile, when they have one.</summary>
    public LandlordProfileId? ViewerLandlordId { get; init; }

    public bool ViewerIsStaff { get; init; }

    /// <summary>Whether the caller's own drafts and private listings are in scope.</summary>
    public bool IncludeNonPublic { get; init; }

    // ── Identity ────────────────────────────────────────────────────────────────────────────

    public IReadOnlyCollection<HousingListingId>? ListingIds { get; init; }

    public LandlordProfileId? LandlordId { get; init; }

    /// <summary>A B7 provider. Correlated against the landlord, never denormalised onto the listing.</summary>
    public Guid? ProviderId { get; init; }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    public IReadOnlyCollection<HousingListingStatus>? Statuses { get; init; }

    /// <summary>Only properties with units left and a move-in date that has arrived.</summary>
    public bool AvailableOnly { get; init; }

    /// <summary>Properties that can be moved into on or before this date.</summary>
    public DateTimeOffset? AvailableBy { get; init; }

    // ── Classification ──────────────────────────────────────────────────────────────────────

    public IReadOnlyCollection<string>? AccommodationTypeKeys { get; init; }

    public IReadOnlyCollection<LeaseType>? LeaseTypes { get; init; }

    /// <summary>At least this furnished. A range, because the enum is ordered.</summary>
    public FurnishedStatus? MinimumFurnishedStatus { get; init; }

    /// <summary>Every one of these must be present, unlike a tag search where any match qualifies.</summary>
    public IReadOnlyCollection<string>? AmenityKeys { get; init; }

    // ── Capacity ────────────────────────────────────────────────────────────────────────────

    public int? MinimumBedrooms { get; init; }

    public int? MaximumBedrooms { get; init; }

    public decimal? MinimumBathrooms { get; init; }

    public int? MinimumOccupants { get; init; }

    // ── Money ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Required for any price narrowing to apply at all. See the class summary.</summary>
    public string? Currency { get; init; }

    /// <summary>Required with a currency: a monthly rent is not comparable with a nightly one.</summary>
    public RentPeriod? Period { get; init; }

    public decimal? MinimumPrice { get; init; }

    public decimal? MaximumPrice { get; init; }

    public bool UtilitiesIncludedOnly { get; init; }

    public bool InternetIncludedOnly { get; init; }

    /// <summary>Properties asking no deposit. What somebody arriving with no savings filters on.</summary>
    public bool NoDepositOnly { get; init; }

    // ── Audience fit ────────────────────────────────────────────────────────────────────────

    public bool StudentFriendlyOnly { get; init; }

    public bool FamilyFriendlyOnly { get; init; }

    public bool ShortStayOnly { get; init; }

    public bool LongStayOnly { get; init; }

    public bool SharedOnly { get; init; }

    /// <summary>Rooms in existing households actively looking for somebody.</summary>
    public bool RoommateWantedOnly { get; init; }

    public bool PetsAllowedOnly { get; init; }

    public bool ParkingOnly { get; init; }

    /// <summary>Properties with at least one amenity flagged as an accessibility feature.</summary>
    public bool AccessibleOnly { get; init; }

    // ── Geography ───────────────────────────────────────────────────────────────────────────

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    /// <summary>B4's area, matched exactly. What the neighbourhood page narrows by.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>
    /// B4's materialised path, matched as a prefix. What "everything under London" is, and an
    /// indexed <c>LIKE 'prefix%'</c> rather than a hierarchy walk.
    /// </summary>
    public string? AreaPathPrefix { get; init; }

    /// <summary>
    /// Alongside <see cref="AreaPathPrefix"/>: a listing filed with <em>no</em> area still matches
    /// when its city carries this name, compared case-insensitively.
    /// <para>
    /// Added 15 Sep 2026. No create screen files a listing under an area, so every listing on the
    /// platform has a null path, and a town search matched as a prefix alone answered nothing at
    /// all. Set only when the path is the town itself — a neighbourhood search must not widen to
    /// every unfiled listing in the town around it.
    /// </para>
    /// </summary>
    public string? AreaPathCityFallback { get; init; }

    /// <summary>
    /// A rectangle, as four edges: a map viewport the client asked for, or — since 15 Sep 2026 —
    /// the rectangle around a radius search's circle, whose corners the search handler trims with
    /// Location's own distance.
    /// <para>
    /// <b>Snapped to <c>PropertyAddress.FuzzedPrecision</c> by the caller.</b> A property is
    /// somebody's home and its coordinate is published rounded; a rectangle that could be
    /// narrowed past that rounding would hand back the exact position one request at a time.
    /// The snapping happens in <c>HousingGeographicScopeAdapter</c>, which is the module's one
    /// translation point, and this filter carries the result rather than repeating the rule.
    /// </para>
    /// </summary>
    public double? BoundsSouth { get; init; }

    public double? BoundsNorth { get; init; }

    public double? BoundsWest { get; init; }

    public double? BoundsEast { get; init; }

    /// <summary>Whether a viewport was supplied at all.</summary>
    public bool HasBounds =>
        BoundsSouth is not null && BoundsNorth is not null
        && BoundsWest is not null && BoundsEast is not null;

    /// <summary>
    /// Whether the rectangle wraps across ±180°, which it does exactly when its western edge is
    /// numerically east of its eastern edge. Without the branch the predicate is unsatisfiable
    /// and a member on the date line gets an empty map with no error.
    /// </summary>
    public bool BoundsCrossAntimeridian => HasBounds && BoundsWest > BoundsEast;

    // ── Trust & moderation ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verified landlords only. Correlated against the landlord profile rather than a flag copied
    /// onto the listing: a badge cached on the listing would keep saying "verified" after Trust
    /// revoked it, which is the platform vouching for exactly the wrong party.
    /// </summary>
    public bool VerifiedLandlordsOnly { get; init; }

    public IReadOnlyCollection<LandlordKind>? LandlordKinds { get; init; }

    /// <summary>The moderation queue's own filters. Narrows an already-empty set for a member.</summary>
    public bool ReportedOnly { get; init; }

    public HousingModerationState? ModerationState { get; init; }

    // ── Text & ordering ─────────────────────────────────────────────────────────────────────

    public string? Text { get; init; }

    public DateTimeOffset? PublishedAfter { get; init; }

    public HousingSortOrder Sort { get; init; } = HousingSortOrder.Newest;

    public int Limit { get; init; } = HousingLimits.DefaultPageSize;

    /// <summary>The page size actually used, clamped to what the module will serve.</summary>
    public int EffectiveLimit => Math.Clamp(Limit, 1, HousingLimits.MaxPageSize);
}
