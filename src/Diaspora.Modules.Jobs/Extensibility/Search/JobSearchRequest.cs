using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Extensibility.Search;

/// <summary>
/// The question a search engine is asked.
/// <para>
/// <b>Filters an engine cannot serve it may ignore.</b> The repository re-applies visibility and
/// every hard filter to the identifiers that come back, so an engine that over-returns costs a
/// wasted row rather than leaking a draft. An engine that under-returns is simply a worse
/// engine, which is a quality problem rather than a correctness one.
/// </para>
/// <para>
/// This is the one place in the Extensibility surface that names domain enums, and it is a
/// deliberate exception: they are the vocabulary the request is expressed in, an adapter switches
/// on their names, and flattening them to strings here would only move the parsing into every
/// adapter.
/// </para>
/// </summary>
public sealed record JobSearchRequest
{
    /// <summary>The free-text term. Null browses rather than searches.</summary>
    public string? Text { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    /// <summary>Location's materialised area path, matched as a prefix.</summary>
    public string? AreaPathPrefix { get; init; }

    /// <summary>
    /// Paths a posting may be filed above <see cref="AreaPathPrefix"/> and still match. Set by a
    /// map viewport; see <c>JobQueryFilter.AreaPathAncestors</c>.
    /// </summary>
    public IReadOnlyCollection<string>? AreaPathAncestors { get; init; }

    public string? CategoryKey { get; init; }

    public string? Tag { get; init; }

    /// <summary>Normalised skill keys. A posting matching any of them qualifies.</summary>
    public IReadOnlyList<string>? SkillKeys { get; init; }

    public Guid? EmployerId { get; init; }

    public IReadOnlyList<JobType>? JobTypes { get; init; }

    public IReadOnlyList<WorkplaceType>? WorkplaceTypes { get; init; }

    public ExperienceLevel? MinimumExperience { get; init; }

    public EducationLevel? MinimumEducation { get; init; }

    public bool VisaSponsorshipOnly { get; init; }

    public bool RelocationAssistanceOnly { get; init; }

    public bool VerifiedEmployersOnly { get; init; }

    public bool FeaturedOnly { get; init; }

    /// <summary>Only postings currently taking applications. True on nearly every real search.</summary>
    public bool OpenOnly { get; init; } = true;

    /// <summary>
    /// The floor, with its currency. An engine must not convert between currencies — a rate has
    /// a date, and a salary silently restated is a number nobody can act on.
    /// </summary>
    public decimal? MinimumSalary { get; init; }

    public string? SalaryCurrency { get; init; }

    public SalaryPeriod? SalaryPeriod { get; init; }

    public DateTimeOffset? PublishedAfter { get; init; }

    public JobSortOrder Sort { get; init; } = JobSortOrder.Relevance;

    /// <summary>Opaque and engine-owned. From a previous outcome's <c>NextCursor</c>.</summary>
    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
