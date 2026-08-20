namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// The whole-document save for a posting's descriptive fields.
/// <para>
/// Wholesale rather than per-field, because that is how the editor works: a recruiter writes and
/// the client saves the form. The handler reports which fields actually moved, so a consumer can
/// ignore a change it does not care about.
/// </para>
/// <para>
/// <see cref="BaseRevision"/> is the optimistic-concurrency token the client received with the
/// posting. Omitting it opts into last-writer-wins, which is occasionally what an internal tool
/// wants and never what an editor does.
/// </para>
/// </summary>
public sealed record UpdateJobRequest
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public string? Summary { get; init; }

    public required string JobType { get; init; }

    public required string ExperienceLevel { get; init; }

    /// <summary><c>None</c> is a real answer and the right one for most skilled-trades roles.</summary>
    public string? EducationLevel { get; init; }

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public Guid? AreaId { get; init; }

    public string? WorkplaceType { get; init; }

    public bool OffersVisaSponsorship { get; init; }

    public bool OffersRelocationAssistance { get; init; }

    /// <summary>Days in the office. Meaningful only for a hybrid arrangement.</summary>
    public int? OnSiteDaysPerWeek { get; init; }

    /// <summary>
    /// Free text qualifying a remote arrangement — "within the EU", "must overlap 09:00–13:00
    /// UTC". The most useful sentence on a remote posting and the one most often absent.
    /// </summary>
    public string? RemotePolicy { get; init; }

    // ── Pay ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Leave both figures null to publish no salary, which is the common case.</summary>
    public decimal? SalaryMinimum { get; init; }

    /// <summary>Omit for a single figure rather than repeating the minimum.</summary>
    public decimal? SalaryMaximum { get; init; }

    /// <summary>ISO 4217. Required whenever a figure is present. Never converted.</summary>
    public string? SalaryCurrency { get; init; }

    /// <summary>Required whenever a figure is present. "50,000" alone is unreadable.</summary>
    public string? SalaryPeriod { get; init; }

    public bool SalaryNegotiable { get; init; }

    // ── Terms ───────────────────────────────────────────────────────────────────────────────

    public string? Visibility { get; init; }

    public int VacancyCount { get; init; } = 1;

    public bool RequiresResume { get; init; }

    /// <summary>The concurrency token from the posting the client loaded.</summary>
    public int? BaseRevision { get; init; }
}
