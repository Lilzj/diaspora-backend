namespace Diaspora.Modules.Creator.Contracts.V1.Requests;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// What a client sends.
//
// Every one of these is deliberately smaller than the response it produces, and in the same way:
// a request carries what the member decided and never what the platform decides. There is no points
// figure on a redemption request, no completion flag on a lesson, no state on an application and no
// award amount an applicant can set. §31 of the phase brief: "A malicious client must not be able to
// POST /rewards/redemptions with points = 999999 and influence the outcome."
//
// Idempotency keys are accepted in the body as well as the Idempotency-Key header, because the
// client sends the body field on every Phase 7 mutation and accepting only the header would have
// meant a client release before a single milestone could be claimed. The header wins where both are
// present.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// CREATOR-GAP-001. The register's DTO exactly: <c>{ bio?, categories?, expertise?, languages?,
/// coverImageUrl?, isDiscoverable? }</c>.
/// <para>
/// <b>Every field is nullable and null means "leave it alone".</b> A client that sent only a bio
/// must not silently clear somebody's categories, which is what a non-nullable collection would do.
/// </para>
/// </summary>
public sealed record UpdateCreatorProfileRequest
{
    public string? Bio { get; init; }

    public IReadOnlyList<string>? Categories { get; init; }

    public IReadOnlyList<string>? Expertise { get; init; }

    public IReadOnlyList<string>? Languages { get; init; }

    public string? CoverImageUrl { get; init; }

    public bool? IsDiscoverable { get; init; }
}

/// <summary>CREATOR-GAP-002. What the member asserts and attaches when they hand the application in.</summary>
public sealed record SubmitCreatorVerificationRequest
{
    /// <summary>
    /// Which requirements the member says they have met.
    /// <para>
    /// <b>Recorded, never trusted.</b> The server re-checks every requirement against real state —
    /// identity verification against Trust, contribution counts against the activity ledger — and
    /// this list exists so a reviewer can see what the member believed.
    /// </para>
    /// </summary>
    public IReadOnlyList<string>? AcknowledgedRequirements { get; init; }

    public IReadOnlyList<Guid>? EvidenceIds { get; init; }

    public string? Note { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// CREATOR-GAP-002. Attaching supporting material.
/// <para>
/// <b>A kind, and nothing else.</b> No file, no URI, no document number — and, since a test caught
/// it, no label either. An earlier version accepted a client-supplied hint and prefixed it with the
/// kind; a client that sent <c>/private/var/tmp/passport-scan.jpg</c> got exactly that stored and
/// served back, which is a local file path on a reviewer's screen and in the database. The register
/// says "a label the server produced", and the only way to mean it is for the client to supply none.
/// </para>
/// </summary>
public sealed record AttachCreatorEvidenceRequest
{
    public required string Kind { get; init; }
}

/// <summary>CREATOR-GAP-002. The member's appeal against a refusal.</summary>
public sealed record AppealCreatorVerificationRequest
{
    public string? Reason { get; init; }
}

/// <summary>
/// A reviewer's decision on a creator-programme application.
/// <para>
/// Separate from the member's requests because it is a different authorization and a different
/// audience: this one names an action and carries the reviewer's words.
/// </para>
/// </summary>
public sealed record DecideCreatorVerificationRequest
{
    /// <summary><c>review</c>, <c>more-information</c>, <c>approve</c>, <c>reject</c>, <c>suspend</c>.</summary>
    public required string Action { get; init; }

    public string? Reason { get; init; }

    public IReadOnlyList<string>? InformationRequested { get; init; }

    public DateTimeOffset? RespondBy { get; init; }
}

/// <summary>CREATOR-GAP-003, -004. A claim carries a key and nothing else.</summary>
public sealed record ClaimRequest
{
    public string? IdempotencyKey { get; init; }
}

/// <summary>CREATOR-GAP-005. Saving a draft application.</summary>
public sealed record SaveGrantApplicationRequest
{
    /// <summary>Keyed by the form's own field keys. Anything else is dropped rather than stored.</summary>
    public IReadOnlyDictionary<string, string>? Answers { get; init; }

    /// <summary>What the applicant asks for, where the programme lets them ask. Minor units.</summary>
    public long? RequestedAmountMinorUnits { get; init; }

    public IReadOnlyList<string>? AttachmentIds { get; init; }

    public IReadOnlyList<string>? AcceptedDeclarations { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// CREATOR-GAP-005. A reviewer's decision on a grant application.
/// <para>
/// <b><see cref="AwardedAmountMinorUnits"/> is the reviewer's figure, bounded by the grant.</b> An
/// approval that took the applicant's requested amount on trust would be the client deciding an
/// award.
/// </para>
/// </summary>
public sealed record DecideGrantApplicationRequest
{
    /// <summary><c>review</c>, <c>more-information</c>, <c>approve</c>, <c>reject</c>, <c>complete</c>.</summary>
    public required string Action { get; init; }

    public long? AwardedAmountMinorUnits { get; init; }

    public string? Reason { get; init; }

    public IReadOnlyList<string>? InformationRequested { get; init; }

    public DateTimeOffset? RespondBy { get; init; }
}

/// <summary>CREATOR-GAP-005. Filing a report against an active grant.</summary>
public sealed record SubmitGrantReportRequest
{
    public required Guid ReportId { get; init; }

    public string? Summary { get; init; }
}

/// <summary>CREATOR-GAP-006. Marking a lesson read. Refused for a quiz lesson.</summary>
public sealed record CompleteLessonRequest
{
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// CREATOR-GAP-006. A quiz submission.
/// <para>
/// One chosen option per question, and no score. The grading is the server's, and the answer key
/// never left it.
/// </para>
/// </summary>
public sealed record SubmitQuizRequest
{
    public required IReadOnlyDictionary<Guid, Guid> Answers { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>CREATOR-GAP-006. Saving or unsaving a course.</summary>
public sealed record SaveCourseRequest
{
    public required bool IsSaved { get; init; }
}

/// <summary>CREATOR-GAP-009. Issuing a referral code where none exists.</summary>
public sealed record IssueReferralCodeRequest
{
    public string? IdempotencyKey { get; init; }
}

/// <summary>CREATOR-GAP-010. Acknowledging the sign-up bonus. Carries no points figure.</summary>
public sealed record AcknowledgeSignupBonusRequest
{
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// CREATOR-GAP-011. Redeeming a reward.
/// <para>
/// <b>A reward identifier and a key. No points figure, no price, no balance.</b> The server loads
/// the authoritative catalogue and charges what it says.
/// </para>
/// </summary>
public sealed record RedeemRewardRequest
{
    public required Guid RewardId { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// A staff decision on a pending redemption.
/// <para>
/// <c>fulfil</c> records what was issued — a placement id, a badge, a booking — in
/// <see cref="GrantReference"/>. <c>reject</c> refunds the points in the same transaction and needs
/// a <see cref="Reason"/>, which the member reads. No points figure: the refund is what was spent.
/// </para>
/// </summary>
public sealed record DecideRedemptionRequest
{
    /// <summary><c>fulfil</c> or <c>reject</c>.</summary>
    public required string Action { get; init; }

    public string? GrantReference { get; init; }

    public string? Reason { get; init; }
}

/// <summary>
/// Makes one of the member's own image uploads their creator cover. Upload first through
/// <c>POST /api/v1/media/uploads/direct</c>, then name the upload here.
/// </summary>
public sealed record SetCreatorCoverRequest
{
    public required Guid MediaId { get; init; }
}
