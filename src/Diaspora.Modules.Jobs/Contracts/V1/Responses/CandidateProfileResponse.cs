namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// A candidate's professional profile.
/// <para>
/// <b>What an employer sees is not what the candidate sees.</b> The mapper omits
/// <see cref="SalaryExpectation"/> entirely for anyone but the owner — handing one side of a
/// negotiation the other side's floor is a thing job products do, and one this platform will not.
/// </para>
/// </summary>
public sealed record CandidateProfileResponse
{
    public required Guid CandidateId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>Null until the candidate writes one. A first application creates a profile without it.</summary>
    public string? Headline { get; init; }

    public string? Summary { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public required string ExperienceLevel { get; init; }

    public required string HighestEducation { get; init; }

    public required string RemotePreference { get; init; }

    public int? NoticePeriodDays { get; init; }

    public required bool IsOpenToWork { get; init; }

    /// <summary>Who can see this profile. Private by default.</summary>
    public required string Visibility { get; init; }

    /// <summary>Never present for anyone but the owner. See the record summary.</summary>
    public CandidateSalaryExpectationResponse? SalaryExpectation { get; init; }

    public required IReadOnlyList<CandidateSkillResponse> Skills { get; init; }

    public required IReadOnlyList<CandidateExperienceResponse> Experience { get; init; }

    public required IReadOnlyList<CandidateEducationResponse> Education { get; init; }

    public required IReadOnlyList<CandidateCertificationResponse> Certifications { get; init; }

    public required IReadOnlyList<CandidateLanguageResponse> Languages { get; init; }

    public required IReadOnlyList<CandidateWorkAuthorizationResponse> WorkAuthorizations { get; init; }

    public required IReadOnlyList<CandidatePreferredLocationResponse> PreferredLocations { get; init; }

    public required IReadOnlyList<CandidatePortfolioItemResponse> Portfolio { get; init; }

    /// <summary>How complete the profile is. The dashboard's nudge; owner-facing.</summary>
    public int? CompletenessPercent { get; init; }

    public required int Revision { get; init; }
}
