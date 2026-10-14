namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// The job search and browse query.
/// <para>
/// Every filter is optional; a request with none is a browse. Unrecognised enum values are
/// ignored rather than refused — an old app version sending a filter the server no longer knows
/// should get results, not a 400.
/// </para>
/// <para>
/// <see cref="MinimumSalary"/> is only applied when <see cref="SalaryCurrency"/> is also present,
/// because nothing in this module converts between currencies. A filter that matched nothing is
/// better than one that compared euros to naira.
/// </para>
/// </summary>
public sealed record SearchJobsRequest
{
    public string? Query { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    /// <summary>
    /// Location's area identifier. Accepted from old app builds and ignored since 14 Oct 2026: a
    /// catalogue node narrows no search.
    /// </summary>
    public Guid? AreaId { get; init; }

    /// <summary>
    /// A point to search around, with <see cref="Longitude"/> and <see cref="RadiusKm"/>: postings
    /// pinned inside the circle, plus unpinned postings in <see cref="CityName"/> when one is given.
    /// </summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Kilometres, or Location's sentinels: <c>-1</c> this town, <c>-2</c> this country.</summary>
    public double? RadiusKm { get; init; }

    /// <summary>
    /// A map viewport: <c>north,south,east,west</c>. Answered by the postings pinned inside it.
    /// </summary>
    public string? Bounds { get; init; }

    public string? CategoryKey { get; init; }

    public string? Tag { get; init; }

    /// <summary>Skill names or keys. Normalised server-side; a posting matching any qualifies.</summary>
    public IReadOnlyList<string>? Skills { get; init; }

    public Guid? EmployerId { get; init; }

    /// <summary><c>FullTime</c>, <c>Contract</c>, …</summary>
    public IReadOnlyList<string>? JobTypes { get; init; }

    /// <summary><c>OnSite</c>, <c>Hybrid</c>, <c>Remote</c>.</summary>
    public IReadOnlyList<string>? WorkplaceTypes { get; init; }

    public string? MinimumExperience { get; init; }

    public string? MaximumExperience { get; init; }

    public string? MinimumEducation { get; init; }

    /// <summary>Only postings that state they sponsor a visa.</summary>
    public bool VisaSponsorshipOnly { get; init; }

    public bool RelocationAssistanceOnly { get; init; }

    /// <summary>Only postings from an employer carrying a Trust mark. The fraud filter.</summary>
    public bool VerifiedEmployersOnly { get; init; }

    public bool FeaturedOnly { get; init; }

    /// <summary>Only postings taking applications. Defaults true — a closed job wastes time.</summary>
    public bool OpenOnly { get; init; } = true;

    public decimal? MinimumSalary { get; init; }

    /// <summary>Required for the salary filter to apply at all.</summary>
    public string? SalaryCurrency { get; init; }

    public string? SalaryPeriod { get; init; }

    public DateTimeOffset? PublishedAfter { get; init; }

    /// <summary><c>Newest</c>, <c>Relevance</c>, <c>SalaryHighToLow</c>, <c>ClosingSoon</c>, …</summary>
    public string? Sort { get; init; }

    /// <summary>A ranking strategy key. Applied over the page the repository returned.</summary>
    public string? Ranking { get; init; }

    /// <summary>Opaque. From a previous response's <c>nextCursor</c>.</summary>
    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
