namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>One standing job alert.</summary>
public sealed record JobAlertResponse
{
    public required Guid AlertId { get; init; }

    public required string Name { get; init; }

    /// <summary><c>Immediate</c>, <c>Daily</c> or <c>Weekly</c>.</summary>
    public required string Frequency { get; init; }

    public required bool IsActive { get; init; }

    public required IReadOnlyList<string> Keywords { get; init; }

    public required IReadOnlyList<string> CountryCodes { get; init; }

    public string? CategoryKey { get; init; }

    public required IReadOnlyList<string> Skills { get; init; }

    public string? WorkplaceType { get; init; }

    public string? JobType { get; init; }

    public string? MinimumExperience { get; init; }

    public required bool VisaSponsorshipOnly { get; init; }

    public required bool VerifiedEmployersOnly { get; init; }

    public decimal? MinimumSalary { get; init; }

    public string? SalaryCurrency { get; init; }

    /// <summary>How many postings this alert has ever surfaced.</summary>
    public required int TotalMatchCount { get; init; }

    public DateTimeOffset? LastNotifiedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
