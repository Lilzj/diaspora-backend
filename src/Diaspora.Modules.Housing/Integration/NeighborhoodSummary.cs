namespace Diaspora.Modules.Housing.Integration;

/// <summary>
/// A neighbourhood as another module sees it.
/// <para>
/// <b>Flat primitives and strings, deliberately.</b> A consumer reads this without referencing a
/// single Housing type — no identifiers, no enums, no value objects — which is what makes the
/// contract safe to depend on: nothing here can be changed by a refactor inside a Housing
/// aggregate. The bands are rendered as their names rather than as an enum for exactly that
/// reason, and <c>null</c> means unassessed rather than lowest.
/// </para>
/// <para>
/// <b>The geographic fields are B4's, passed through.</b> They are here so a consumer can render a
/// chip without a second call, not because Housing owns them. A consumer that needs authoritative
/// geography — a parent city, a boundary, a distance — asks Location.
/// </para>
/// </summary>
public sealed record NeighborhoodSummary
{
    /// <summary>B4's area. The identifier every module already shares.</summary>
    public required Guid AreaId { get; init; }

    /// <summary>B4's name, passed through.</summary>
    public required string Name { get; init; }

    /// <summary>B4's slug, which is how the client's neighbourhood route addresses places.</summary>
    public required string Slug { get; init; }

    public required string CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? RegionName { get; init; }

    // ── The part Housing owns ───────────────────────────────────────────────────────────────

    /// <summary>One or two sentences. What a card renders when it has room for prose.</summary>
    public string? Description { get; init; }

    /// <summary>Band names, or null where nobody has assessed them. Never a number.</summary>
    public string? Walkability { get; init; }

    public string? SafetyBand { get; init; }

    public string? CostOfLivingBand { get; init; }

    public string? NoiseLevel { get; init; }

    public string? FamilyFriendliness { get; init; }

    public string? StudentFriendliness { get; init; }

    public string? InternetAvailability { get; init; }

    /// <summary>
    /// Languages you will hear, most prominent first. The single most useful field here for a
    /// member deciding whether they will be understood at the chemist.
    /// </summary>
    public IReadOnlyList<string> PopularLanguages { get; init; } = [];

    /// <summary>Published community insights.</summary>
    public int InsightCount { get; init; }

    /// <summary>Live housing listings. What a discovery strip orders by.</summary>
    public int ListingCount { get; init; }

    /// <summary>Whether an approver has signed the profile off.</summary>
    public bool IsApproved { get; init; }

    /// <summary>
    /// How complete the profile is, nought to one. Rendered as a prompt to contribute rather than
    /// as a quality score: a thin profile is a place nobody has written about, not a bad place.
    /// </summary>
    public double Completeness { get; init; }

    /// <summary>When it was last reviewed. Prose about a place goes stale.</summary>
    public DateTimeOffset? LastReviewedAt { get; init; }
}
