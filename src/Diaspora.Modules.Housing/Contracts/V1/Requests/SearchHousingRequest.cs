using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// One housing search, as a client expresses it.
/// <para>
/// <b>Bound from the query string, so every field is a nullable primitive or a list of them.</b> No
/// domain type appears here: the handler translates into <c>HousingListingQueryFilter</c> once, and
/// that translation is the only place a client's loose parameters meet the module's vocabulary.
/// </para>
/// <para>
/// <b>A rent range needs a currency and a period to mean anything, and the validator says so.</b>
/// Nothing in this module converts a currency or normalises a rent period, so a range without both
/// narrows nothing rather than comparing a nightly hostel rate against a monthly flat.
/// </para>
/// <para>
/// <b>The repeated parameters are arrays and are attributed, and both details are load-bearing.</b>
/// Minimal APIs bind <c>string[]</c> from a query string and infer <c>IReadOnlyList&lt;string&gt;</c>
/// as a request body — which a GET refuses outright, at startup, by throwing while the route is
/// being mapped. The failure is not local: it takes the whole host down, so every integration test
/// in the suite fails with a disposed service provider and none of them mentions housing. This is
/// the shape to remember.
/// </para>
/// </summary>
public sealed record SearchHousingRequest
{
    /// <summary>Free text over titles, summaries and place names.</summary>
    public string? Query { get; init; }

    // ── Where ───────────────────────────────────────────────────────────────────────────────

    public string? CountryCode { get; init; }

    public string? City { get; init; }

    /// <summary>B4's area at neighbourhood level. What the neighbourhood page narrows by.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>Any B4 area. Resolved to a subtree, so a city returns every neighbourhood in it.</summary>
    public Guid? AreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Kilometres. Resolved by B4, which owns every radius sentinel the client may send.</summary>
    public double? RadiusKm { get; init; }

    /// <summary>
    /// A map viewport: <c>north,south,east,west</c>. Wins over the point-and-radius pair.
    /// <para>
    /// Parsed by B4 — including the ±180° wrap. Properties are matched against the rectangle at
    /// the precision their address is published at, so a viewport cannot be narrowed around one
    /// home until only that home answers.
    /// </para>
    /// </summary>
    public string? Bounds { get; init; }

    // ── What ────────────────────────────────────────────────────────────────────────────────

    [FromQuery]
    public string[]? AccommodationTypes { get; init; }

    [FromQuery]
    public string[]? LeaseTypes { get; init; }

    /// <summary>Every one must be present, unlike a tag search where any match qualifies.</summary>
    [FromQuery]
    public string[]? Amenities { get; init; }

    public int? MinBedrooms { get; init; }

    public int? MaxBedrooms { get; init; }

    public decimal? MinBathrooms { get; init; }

    public int? MinOccupants { get; init; }

    /// <summary>At least this furnished. A range, because the values are ordered.</summary>
    public string? MinFurnishedStatus { get; init; }

    // ── Money ───────────────────────────────────────────────────────────────────────────────

    public decimal? MinRent { get; init; }

    public decimal? MaxRent { get; init; }

    /// <summary>Required for any rent narrowing. See the class summary.</summary>
    public string? Currency { get; init; }

    /// <summary>Required for any rent narrowing. A monthly rent is not comparable with a nightly one.</summary>
    public string? RentPeriod { get; init; }

    public bool? UtilitiesIncluded { get; init; }

    public bool? InternetIncluded { get; init; }

    /// <summary>Properties asking no deposit. What somebody arriving with no savings filters on.</summary>
    public bool? NoDeposit { get; init; }

    // ── Who for ─────────────────────────────────────────────────────────────────────────────

    public bool? StudentFriendly { get; init; }

    public bool? FamilyFriendly { get; init; }

    public bool? ShortStay { get; init; }

    public bool? LongStay { get; init; }

    public bool? Shared { get; init; }

    public bool? RoommateWanted { get; init; }

    public bool? PetsAllowed { get; init; }

    public bool? Parking { get; init; }

    /// <summary>Properties with an accessibility feature. Not a convenience filter.</summary>
    public bool? Accessible { get; init; }

    // ── When ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Ready to move into by this date. What somebody with a flight booked filters on.</summary>
    public DateTimeOffset? AvailableBy { get; init; }

    public bool? AvailableOnly { get; init; }

    // ── Trust ───────────────────────────────────────────────────────────────────────────────

    public bool? VerifiedOnly { get; init; }

    [FromQuery]
    public string[]? LandlordKinds { get; init; }

    // ── Presentation ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Newest, PriceLowToHigh, PriceHighToLow, MostViewed, AvailableSoonest, Relevance, Nearest or
    /// Verified.
    /// </summary>
    public string? Sort { get; init; }

    /// <summary>A ranking strategy key applied to the page after the database has ordered it.</summary>
    public string? Ranking { get; init; }

    /// <summary>
    /// Nullable, and not because absence means anything — it means false either way.
    /// <para>
    /// Minimal APIs treat a non-nullable value type as a <em>required</em> query parameter, so a
    /// plain <c>bool</c> here turns the plainest search link on the platform into a 400 with an
    /// empty body: no validation message, nothing in the logs above a debug line, and a client with
    /// no way to work out what it left out. B9 recorded this exact shape and B11 repeated it
    /// anyway; the assertion in <c>HousingModuleTests.Search_answers_without_a_token</c> is what
    /// caught it.
    /// </para>
    /// </summary>
    public bool? IncludeFacets { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
