namespace Diaspora.Modules.Jobs.Extensibility.Search;

/// <summary>
/// One posting, flattened for an index.
/// <para>
/// <b>Every field is a primitive, a string or a list of strings, and that is the contract.</b>
/// An engine adapter must be able to serialise this without referencing a single Jobs type — no
/// value objects, no strongly typed identifiers, no domain enums. The moment one leaked in, an
/// OpenSearch adapter would need a project reference to this module's domain, and the
/// independence the port exists for would be gone.
/// </para>
/// <para>
/// It carries denormalised employer fields because a search result is ranked and filtered on
/// them — "verified employers only" is the filter this audience reaches for first, and joining
/// to answer it at query time is exactly what an index exists to avoid.
/// </para>
/// </summary>
public sealed record JobSearchDocument
{
    public required Guid JobId { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    /// <summary>The full posting text, for engines that index it. Long.</summary>
    public string? Body { get; init; }

    // ── Employer, denormalised ──────────────────────────────────────────────────────────────

    public required Guid EmployerId { get; init; }

    public required string EmployerName { get; init; }

    /// <summary>Whether the employer carried a Trust mark when this was indexed.</summary>
    public required bool IsEmployerVerified { get; init; }

    /// <summary>Whether the employer is an agency or staffing firm rather than the hirer.</summary>
    public required bool IsIntermediary { get; init; }

    // ── Classification ──────────────────────────────────────────────────────────────────────

    public required string CategoryKey { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>Normalised skill keys, matching the candidate side's normalisation exactly.</summary>
    public required IReadOnlyList<string> SkillKeys { get; init; }

    /// <summary>Display names for the same skills, for engines that index prose.</summary>
    public required IReadOnlyList<string> SkillNames { get; init; }

    public required string JobType { get; init; }

    public required string ExperienceLevel { get; init; }

    public required string EducationLevel { get; init; }

    // ── Where ───────────────────────────────────────────────────────────────────────────────

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary>Location's materialised area path, for prefix-scoped queries.</summary>
    public string? AreaPath { get; init; }

    public required string WorkplaceType { get; init; }

    public required bool IsRemote { get; init; }

    public required bool OffersVisaSponsorship { get; init; }

    public required bool OffersRelocationAssistance { get; init; }

    // ── Pay ─────────────────────────────────────────────────────────────────────────────────

    public required bool IsSalaryDisclosed { get; init; }

    public decimal? SalaryMinimum { get; init; }

    public decimal? SalaryMaximum { get; init; }

    /// <summary>ISO 4217. An engine must not convert; the currency is part of the filter.</summary>
    public string? SalaryCurrency { get; init; }

    public string? SalaryPeriod { get; init; }

    // ── State & signals ─────────────────────────────────────────────────────────────────────

    public required string Status { get; init; }

    /// <summary>Whether the posting is currently taking applications.</summary>
    public required bool IsOpen { get; init; }

    public required bool IsFeatured { get; init; }

    public required bool IsVerified { get; init; }

    public required int VacancyCount { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    public DateTimeOffset? ClosingAt { get; init; }

    /// <summary>Ranking signals. An engine may use them or ignore them.</summary>
    public required long ViewCount { get; init; }

    public required int ApplicationCount { get; init; }
}
