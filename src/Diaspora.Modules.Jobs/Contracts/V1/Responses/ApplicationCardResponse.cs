namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One application in a list — the employer's applicant list and the candidate's tracker, which
/// are the same query read from opposite ends.
/// <para>
/// Carries no history: an applicant list of twenty rows must not drag several hundred timeline
/// entries with it, and no list renders them.
/// </para>
/// <para>
/// <see cref="HasBeenViewed"/> and <see cref="DaysWaiting"/> are the two fields that make a
/// candidate's tracker honest. An application sitting unopened for three weeks is exactly what
/// the platform should be telling somebody about.
/// </para>
/// </summary>
public sealed record ApplicationCardResponse
{
    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required string JobTitle { get; init; }

    public required string JobSlug { get; init; }

    public required Guid EmployerId { get; init; }

    public required string EmployerName { get; init; }

    public Guid? EmployerLogoMediaId { get; init; }

    public required Guid CandidateId { get; init; }

    /// <summary>
    /// The applicant's headline, when they have written one. Employer's list only, and optional:
    /// a member's first application creates a profile without one.
    /// </summary>
    public string? CandidateHeadline { get; init; }

    /// <summary>
    /// Who applied, by the name on their Diaspora account. Employer's list only.
    /// <para>
    /// Added 14 Sep 2026. Until then the employer's list carried the headline and nothing else, so
    /// an applicant who had written none reached the employer as "Applicant". Null on the
    /// candidate's own tracker, and when the account can no longer be found — an erased member is
    /// not named.
    /// </para>
    /// </summary>
    public string? ApplicantName { get; init; }

    /// <summary>
    /// Whether the applicant's account is verified now. Employer's list only; null wherever
    /// <see cref="ApplicantName"/> is, because an unanswered question is not "unverified".
    /// </summary>
    public bool? ApplicantIsVerified { get; init; }

    public required string Status { get; init; }

    public string? StageKey { get; init; }

    public required DateTimeOffset SubmittedAt { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    /// <summary>Whether a human at the employer has opened it.</summary>
    public required bool HasBeenViewed { get; init; }

    /// <summary>Whole days since submission with no decision. Null once decided.</summary>
    public int? DaysWaiting { get; init; }

    public required bool IsActive { get; init; }

    public required int AttachmentCount { get; init; }
}
