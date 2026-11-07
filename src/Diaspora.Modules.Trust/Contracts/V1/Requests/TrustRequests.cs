namespace Diaspora.Modules.Trust.Contracts.V1.Requests;

/// <summary>
/// Files an appeal.
/// <para>
/// Accepts the frontend's inline body from <c>appeals.tsx:40</c> — <c>{ type, targetId,
/// reason }</c> — unchanged, and adds the three optional fields the frontend's separate
/// <c>AppealSubmissionRequest</c> type carries. That is what lets one endpoint replace the
/// two the mocks declare (<c>/trust/appeals</c> and <c>/trust/verify/diaspora/appeal</c>)
/// without either caller changing its payload.
/// </para>
/// </summary>
public sealed record SubmitAppealRequest
{
    /// <summary>Verification, Content, Suspension, Publishing.</summary>
    public required string Type { get; init; }

    /// <summary>
    /// What is being appealed. For a verification appeal this is the verification id —
    /// which is how the frontend's <c>AppealSubmissionRequest.verificationId</c> maps here.
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>The member's argument. The frontend calls this <c>explanation</c> in one
    /// type and <c>reason</c> in the other; <c>reason</c> is the one actually posted.</summary>
    public required string Reason { get; init; }

    /// <summary>Storage keys for supporting evidence. At most ten.</summary>
    public IReadOnlyList<string>? AdditionalDocuments { get; init; }

    /// <summary>Whether this is a correction request rather than a reversal request.</summary>
    public bool CorrectionRequest { get; init; }
}

/// <summary>A reviewer's decision on an appeal. Staff only.</summary>
public sealed record ReviewAppealRequest
{
    /// <summary>Approve, Reject, Escalate.</summary>
    public required string Decision { get; init; }

    /// <summary>Shown to the member as reviewer comments. Required.</summary>
    public required string Explanation { get; init; }
}

/// <summary>
/// Records a community confirmation on someone else's contribution.
/// <para>
/// Replaces the frontend's <c>POST /trust/community-voting</c>, whose <c>{ updateId }</c>
/// body maps to <see cref="TargetType"/> = <c>"community-update"</c> plus
/// <see cref="TargetId"/>. Renamed because nothing is tallied toward a majority — it is a
/// single peer confirmation feeding the recipient's trust dimension.
/// </para>
/// </summary>
public sealed record RecordConfirmationRequest
{
    /// <summary>What kind of thing — "community-update", "guide".</summary>
    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    /// <summary>Who wrote it, and therefore earns the points.</summary>
    public required Guid AuthorUserId { get; init; }

    /// <summary>Which country community this counts toward.</summary>
    public string? CountryCode { get; init; }

    public string? Note { get; init; }
}

/// <summary>
/// Asks whether the caller may take an action.
/// <para>
/// The server-side replacement for <c>TrustEngine.evaluateAction</c>. The client keeps its
/// local copy for instant UI feedback; this is the answer that decides.
/// </para>
/// </summary>
public sealed record EvaluateEligibilityRequest
{
    /// <summary>A capability key — "guides", "jobs", "housing", "marketplace", "comment".</summary>
    public required string Capability { get; init; }

    /// <summary>The country the action targets, when country-scoped.</summary>
    public string? TargetCountry { get; init; }

    /// <summary>
    /// When true the rate-limit counter is checked but not incremented, so a screen can grey
    /// out a button without consuming the member's quota. Mirrors the client's
    /// <c>dryRun</c> parameter, which defaults to true for exactly this reason.
    /// </summary>
    public bool DryRun { get; init; } = true;
}

/// <summary>Takes a moderation queue item onto the caller's desk.</summary>
public sealed record AssignQueueItemRequest
{
    /// <summary>Omit to take it yourself. Naming someone else needs queue-management rights.</summary>
    public Guid? ModeratorUserId { get; init; }
}

/// <summary>Records a moderator's decision on a queue item.</summary>
public sealed record RecordModerationDecisionRequest
{
    /// <summary>NoAction, HidePendingReview, Remove (a proposal a second member of staff confirms), Restore.</summary>
    public required string Outcome { get; init; }

    /// <summary>A reason code: FalseOrMisleading, FraudOrScam, AsksForFees, and so on.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>What the owner reads. At least ten characters.</summary>
    public required string Note { get; init; }
}

/// <summary>A second member of staff checks the first one's decision.</summary>
public sealed record ModerationSecondReviewRequest
{
    /// <summary>True to confirm the decision; false to put your own choice in its place.</summary>
    public required bool Confirm { get; init; }

    /// <summary>When declining a removal: HidePendingReview, NoAction or Restore.</summary>
    public string? Instead { get; init; }

    public required string Note { get; init; }
}

/// <summary>Staff open a case with no report, because they found the problem first.</summary>
public sealed record OpenModerationCaseRequest
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required string ReasonCode { get; init; }

    /// <summary>What was found, and where. A second member of staff checks it within 7 days.</summary>
    public required string Note { get; init; }
}

/// <summary>Awards or revokes a badge outside the automatic rules. Admin only.</summary>
public sealed record ManageBadgeRequest
{
    public required Guid UserId { get; init; }

    public required string Badge { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>True to award, false to revoke.</summary>
    public required bool Grant { get; init; }

    /// <summary>Required when revoking.</summary>
    public string? Reason { get; init; }
}
