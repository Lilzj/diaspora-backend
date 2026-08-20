namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// The whole-document save for a candidate's headline, location and working preferences.
/// <para>
/// The salary expectation is here because the candidate uses it to filter their own searches. It
/// is <b>never disclosed to an employer</b> and never searchable — handing one side of a
/// negotiation the other side's floor is a thing job products do, and one this platform will not.
/// </para>
/// </summary>
public sealed record UpdateCandidateProfileRequest
{
    public required string Headline { get; init; }

    public string? Summary { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    /// <summary>Where the candidate places themselves. Their claim, not a computed figure.</summary>
    public required string ExperienceLevel { get; init; }

    /// <summary><c>Flexible</c>, <c>OnSiteOnly</c>, <c>HybridPreferred</c>, <c>RemoteOnly</c>.</summary>
    public string? RemotePreference { get; init; }

    /// <summary>Never shown to an employer. A filter input on the candidate's own searches.</summary>
    public decimal? ExpectedSalaryMinimum { get; init; }

    public string? ExpectedSalaryCurrency { get; init; }

    public string? ExpectedSalaryPeriod { get; init; }

    public bool ExpectedSalaryFlexible { get; init; }

    /// <summary>How soon they could start, in days.</summary>
    public int? NoticePeriodDays { get; init; }

    public bool IsOpenToWork { get; init; } = true;

    public int? BaseRevision { get; init; }
}
