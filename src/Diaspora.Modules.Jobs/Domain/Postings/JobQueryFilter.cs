using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// Everything a posting listing can narrow on, in one record.
/// <para>
/// <b>One filter type for search, browse, nearby, the employer dashboard and the candidate's
/// saved list.</b> They are the same query with different fields set, and expressing them as
/// five repository methods would mean five places to add a filter and four of them forgotten.
/// The repository translates whatever is present and ignores the rest.
/// </para>
/// <para>
/// <b>The audience fields at the bottom are what keep drafts private.</b> A repository applies
/// visibility from <see cref="ViewerEmployerId"/> and <see cref="IncludeNonPublic"/>, in one
/// place, rather than trusting every caller to remember — which is how a draft ends up in a
/// public result set exactly once, permanently, in production.
/// </para>
/// </summary>
public sealed record JobQueryFilter
{
    /// <summary>Free text. Matched against title, summary, employer name and skills.</summary>
    public string? Text { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; init; }

    /// <summary>A city label, matched case-insensitively against the denormalised name.</summary>
    public string? CityName { get; init; }

    /// <summary>Location's materialised area path, matched as a prefix.</summary>
    public string? AreaPathPrefix { get; init; }

    /// <summary>
    /// Paths a posting may be filed <em>above</em> the narrowed subtree and still match.
    /// <para>
    /// Set only by a map viewport, and the reason it exists: a job in London is filed against
    /// London, and a member looking at one street of Peckham would otherwise see none of them.
    /// Containment runs both ways and only one way is a prefix — the other is an <c>IN</c> over
    /// a short list the caller already has, because EF Core does not translate a
    /// parameter-receiver <c>StartsWith</c> and the query would compile, read correctly and throw
    /// the first time anybody ran it.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<string>? AreaPathAncestors { get; init; }

    /// <summary>
    /// A rectangle over the posting's own coordinates: a map viewport, or the rectangle around a
    /// circle search (14 Oct 2026). A posting with no coordinates is outside it unless it names
    /// <see cref="UnpinnedCityName"/>.
    /// </summary>
    public double? MinLatitude { get; init; }

    public double? MaxLatitude { get; init; }

    public double? MinLongitude { get; init; }

    public double? MaxLongitude { get; init; }

    /// <summary>
    /// With a rectangle: a town whose unpinned postings are kept, compared case-insensitively.
    /// Postings predate the pin, and a circle search that also named a town still finds them.
    /// </summary>
    public string? UnpinnedCityName { get; init; }

    /// <summary>Whether all four edges of the rectangle are set.</summary>
    public bool HasBoundingBox =>
        MinLatitude is not null && MaxLatitude is not null
        && MinLongitude is not null && MaxLongitude is not null;

    /// <summary>Whether the rectangle wraps across ±180°.</summary>
    public bool BoundingBoxCrossesAntimeridian => HasBoundingBox && MinLongitude > MaxLongitude;

    public string? CategoryKey { get; init; }

    /// <summary>A normalised tag slug.</summary>
    public string? Tag { get; init; }

    /// <summary>Normalised skill keys. A posting matching any of them qualifies.</summary>
    public IReadOnlyList<string>? SkillKeys { get; init; }

    public EmployerProfileId? EmployerId { get; init; }

    public IReadOnlyList<JobType>? JobTypes { get; init; }

    public IReadOnlyList<WorkplaceType>? WorkplaceTypes { get; init; }

    public ExperienceLevel? MinimumExperience { get; init; }

    public ExperienceLevel? MaximumExperience { get; init; }

    public EducationLevel? MinimumEducation { get; init; }

    /// <summary>Only postings that state they sponsor a visa.</summary>
    public bool VisaSponsorshipOnly { get; init; }

    /// <summary>Only postings that state they help with relocation.</summary>
    public bool RelocationAssistanceOnly { get; init; }

    /// <summary>Only postings from an employer carrying a Trust mark.</summary>
    public bool VerifiedEmployersOnly { get; init; }

    public bool FeaturedOnly { get; init; }

    /// <summary>Only postings currently taking applications.</summary>
    public bool OpenOnly { get; init; }

    /// <summary>
    /// The lowest advertised figure to accept, with its currency. Applied only to postings
    /// quoting the same currency — no conversion happens anywhere in this module, and a filter
    /// that silently compared euros to naira would be worse than one that matched nothing.
    /// </summary>
    public decimal? MinimumSalary { get; init; }

    public string? SalaryCurrency { get; init; }

    public SalaryPeriod? SalaryPeriod { get; init; }

    /// <summary>Only postings published at or after this instant.</summary>
    public DateTimeOffset? PublishedAfter { get; init; }

    /// <summary>Restrict to these specific postings. How a search engine's hits are hydrated.</summary>
    public IReadOnlyList<JobPostingId>? PostingIds { get; init; }

    /// <summary>Restrict to these statuses. The employer dashboard's tabs.</summary>
    public IReadOnlyList<JobStatus>? Statuses { get; init; }

    public JobSortOrder Sort { get; init; } = JobSortOrder.Newest;

    // ── Audience ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The employer whose own drafts and private postings this caller may see. Null for
    /// everyone who is not looking at their own dashboard.
    /// </summary>
    public EmployerProfileId? ViewerEmployerId { get; init; }

    /// <summary>The signed-in member, for surfaces that annotate rows with "you applied".</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>
    /// Whether drafts, private and hidden postings are in scope. Staff and the owning
    /// employer's dashboard only.
    /// </summary>
    public bool IncludeNonPublic { get; init; }

    /// <summary>Whether the caller holds a platform staff role, and so sees everything.</summary>
    public bool IsStaff { get; init; }
}
