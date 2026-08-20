namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// One country a member has lived in.
/// <para>
/// A superset of two frontend types that describe the same thing from different angles:
/// <c>CountryExperienceInterval</c> (countryCode, countryName, flag, startYear, endYear,
/// isVerified, role) from the trust store, and the declared half of
/// <c>CountryVerificationSummary</c> (arrivalYear, yearsOfResidence, expertiseAreas). Both
/// bind unchanged.
/// </para>
/// <para>
/// <c>isVerified</c> appears here but is not Location's answer. It is Trust's decision,
/// projected from Trust's published events and joined at read time — the boundary described
/// in section 8 of the frontend contract report. Location owns the claim; Trust owns the proof.
/// </para>
/// </summary>
public sealed record CountryExperienceResponse
{
    public required Guid Id { get; init; }

    public required string CountryCode { get; init; }

    public required string CountryName { get; init; }

    public required string Flag { get; init; }

    public required DateOnly ArrivedOn { get; init; }

    /// <summary>Null while the member still lives there — the timeline's "Present".</summary>
    public DateOnly? DepartedOn { get; init; }

    /// <summary>The frontend's <c>startYear</c>.</summary>
    public required int StartYear { get; init; }

    /// <summary>The frontend's <c>endYear</c>. Null means present.</summary>
    public int? EndYear { get; init; }

    /// <summary>The frontend's <c>yearsOfResidence</c>, computed as at today.</summary>
    public required int Years { get; init; }

    public required bool IsCurrentResidence { get; init; }

    /// <summary><c>Newcomer</c>, <c>Settling</c>, <c>Established</c>, <c>Seasoned</c>,
    /// <c>Native</c>. Derived from the period, never declared.</summary>
    public required string ExperienceLevel { get; init; }

    /// <summary>What the member was there as. Free text; rendered verbatim.</summary>
    public string? Role { get; init; }

    public required string LanguageFamiliarity { get; init; }

    public required string CulturalFamiliarity { get; init; }

    /// <summary>Trust's decision. Not computed here.</summary>
    public required bool IsVerified { get; init; }

    /// <summary>
    /// <c>Unverified</c>, <c>UnderReview</c>, <c>Verified</c>, <c>Rejected</c>,
    /// <c>Expired</c>, <c>Revoked</c>. The fuller state behind <c>isVerified</c>, so a client
    /// can show "under review" rather than a bare unverified.
    /// </summary>
    public required string VerificationState { get; init; }

    public required IReadOnlyList<string> ExpertiseAreas { get; init; }
}
