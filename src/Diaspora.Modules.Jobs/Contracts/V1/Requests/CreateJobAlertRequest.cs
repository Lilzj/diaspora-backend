namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Creates or updates a standing job alert.
/// <para>
/// At least one criterion is required. An alert with none matches every posting on the platform,
/// which is not an alert — it is a way to make the platform send a hundred notifications a day
/// until the member turns notifications off entirely.
/// </para>
/// </summary>
public sealed record CreateJobAlertRequest
{
    /// <summary>What the candidate calls it. "Nursing, Germany, sponsored".</summary>
    public required string Name { get; init; }

    /// <summary><c>Immediate</c>, <c>Daily</c> or <c>Weekly</c>.</summary>
    public string? Frequency { get; init; }

    public IReadOnlyList<string>? Keywords { get; init; }

    public IReadOnlyList<string>? CountryCodes { get; init; }

    public string? CategoryKey { get; init; }

    public IReadOnlyList<string>? Skills { get; init; }

    public string? WorkplaceType { get; init; }

    public string? JobType { get; init; }

    public string? MinimumExperience { get; init; }

    public bool VisaSponsorshipOnly { get; init; }

    public bool VerifiedEmployersOnly { get; init; }

    public decimal? MinimumSalary { get; init; }

    /// <summary>Required whenever a minimum is set.</summary>
    public string? SalaryCurrency { get; init; }
}
