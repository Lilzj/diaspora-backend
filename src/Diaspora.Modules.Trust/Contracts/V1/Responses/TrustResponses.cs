namespace Diaspora.Modules.Trust.Contracts.V1.Responses;

/// <summary>
/// An appeal. Matches the frontend's <c>AppealRequest</c>
/// (which is a response shape despite the name).
/// </summary>
public sealed record AppealResponse
{
    public required Guid AppealId { get; init; }

    /// <summary>Verification, Content, Suspension, Publishing, RewardHold, Money.</summary>
    public required string Type { get; init; }

    public required string TargetId { get; init; }

    public required string Reason { get; init; }

    /// <summary>
    /// Pending, UnderReview, Escalated, Approved, Rejected, Withdrawn.
    /// <para>
    /// The client's own union is only the first, fourth and fifth. The extra states are what
    /// its six-stage workflow tracker already draws but had no data for.
    /// </para>
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Reviewer comments, shown to the member. Never empty.</summary>
    public string? Explanation { get; init; }

    public required bool IsCorrectionRequest { get; init; }

    public required int EvidenceCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }
}

/// <summary>An appeal with its review history. Detail route only.</summary>
public sealed record AppealDetailResponse
{
    public required AppealResponse Appeal { get; init; }

    public required IReadOnlyList<AppealReviewResponse> Reviews { get; init; }
}

/// <summary>One step in an appeal's history.</summary>
public sealed record AppealReviewResponse
{
    public required Guid ReviewId { get; init; }

    /// <summary>Null in a member-facing projection — reviewers are anonymised to members.</summary>
    public Guid? ReviewerId { get; init; }

    public required string FromStatus { get; init; }

    public required string ToStatus { get; init; }

    public string? Notes { get; init; }

    public required DateTimeOffset ReviewedAt { get; init; }
}

/// <summary>
/// The answer to "may I do this?".
/// <para>
/// <see cref="Code"/> carries the exact literals the client's <c>TrustEngine</c> already
/// returns — ALLOW, DENIED, RATE_LIMITED, COOLDOWN, HEALTH_RESTRICTED — so the screens that
/// branch on them keep working once the decision moves server-side. That vocabulary surviving
/// the move is the whole reason the migration is cheap.
/// </para>
/// </summary>
public sealed record EligibilityDecisionResponse
{
    public required bool Allowed { get; init; }

    public required string Code { get; init; }

    public string? Reason { get; init; }

    /// <summary>Seconds to wait. Set for RATE_LIMITED and COOLDOWN.</summary>
    public int? WaitTimeSec { get; init; }

    /// <summary>Named requirements not yet met. Rendered as a checklist.</summary>
    public IReadOnlyList<string> UnmetRequirements { get; init; } = [];
}

/// <summary>
/// One capability's unlock progress. Matches the frontend's
/// <c>FeatureUnlockRequirement</c>, which its Feature Gates tab renders directly.
/// </summary>
public sealed record FeatureRequirementResponse
{
    /// <summary>The capability key — "guides", "jobs".</summary>
    public required string FeatureId { get; init; }

    public required string Name { get; init; }

    public required bool IsUnlocked { get; init; }

    public required IReadOnlyList<RequirementItemResponse> Requirements { get; init; }
}

/// <summary>Matches the frontend's <c>FeatureRequirementItem</c>.</summary>
public sealed record RequirementItemResponse
{
    public required string Description { get; init; }

    public required bool IsMet { get; init; }

    /// <summary>Progress text — "45 / 150 pts", "Verified".</summary>
    public required string CurrentProgress { get; init; }
}

/// <summary>
/// One item in the moderation queue. Matches the frontend's <c>ModerationQueueItem</c>.
/// </summary>
public sealed record ModerationQueueItemResponse
{
    public required Guid ItemId { get; init; }

    /// <summary>Guide, CommunityUpdate, Comment, Listing, Profile, Organization.</summary>
    public required string ContentType { get; init; }

    public required string ContentId { get; init; }

    public string? Title { get; init; }

    public required Guid AuthorId { get; init; }

    /// <summary>The author's reputation level, so a moderator can weigh the item.</summary>
    public string? AuthorLevel { get; init; }

    public required int AuthorViolationCount { get; init; }

    public AiReviewResponse? AiReview { get; init; }

    public required int ReportCount { get; init; }

    public required string Status { get; init; }

    /// <summary>Low, Normal, High, Urgent.</summary>
    public required string Priority { get; init; }

    public Guid? AssignedModeratorId { get; init; }

    public DateTimeOffset? DueAt { get; init; }

    public required bool IsOverdue { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    // ── The case (7 Nov 2026) ───────────────────────────────────────────

    /// <summary>Which module's content: job, housing-listing, guide, and so on.</summary>
    public string? TargetType { get; init; }

    public string? Area { get; init; }

    public string? ReasonCode { get; init; }

    public string? ReasonLabel { get; init; }

    public bool StaffInitiated { get; init; }

    public string? OpeningNote { get; init; }

    /// <summary>What has been done to the content so far. Null until somebody decides.</summary>
    public string? CurrentOutcome { get; init; }

    /// <summary>None, EmergencyHide, StaffInitiated or RemovalProposal.</summary>
    public string SecondReview { get; init; } = "None";

    public DateTimeOffset? SecondReviewDueAt { get; init; }

    /// <summary>Who must not do the second review.</summary>
    public Guid? FirstDeciderId { get; init; }

    public string? ProposedReasonCode { get; init; }

    public string? ProposedNote { get; init; }

    public DateTimeOffset? AppealDeadline { get; init; }

    public DateTimeOffset? ReviewRequestedAt { get; init; }

    public bool HiddenAutomatically { get; init; }

    /// <summary>Whether a moderator may decide it now.</summary>
    public bool AwaitsDecision { get; init; }

    /// <summary>The reports, without who made them.</summary>
    public IReadOnlyList<ModerationCaseReportResponse> Reports { get; init; } = [];

    public IReadOnlyList<ModerationDecisionResponse> Decisions { get; init; } = [];
}

/// <summary>One report on a case, as staff read it: the reason and words, not the reporter.</summary>
public sealed record ModerationCaseReportResponse
{
    public required string ReasonCode { get; init; }

    public required string ModuleReason { get; init; }

    public string? Detail { get; init; }

    public required string Module { get; init; }

    public required DateTimeOffset FiledAt { get; init; }
}

/// <summary>An automated pre-screen. Matches the frontend's <c>AIReview</c>.</summary>
public sealed record AiReviewResponse
{
    /// <summary>Clean, Flagged.</summary>
    public required string Status { get; init; }

    /// <summary>snake_case labels — "spam", "hate_speech".</summary>
    public required IReadOnlyList<string> Flags { get; init; }

    /// <summary>0–1.</summary>
    public required decimal Confidence { get; init; }

    public required DateTimeOffset ProcessedAt { get; init; }
}

/// <summary>A moderator's recorded decision. Matches the frontend's <c>ModerationAction</c>.</summary>
public sealed record ModerationDecisionResponse
{
    public required Guid ActionId { get; init; }

    /// <summary>Null when the system acted.</summary>
    public Guid? ModeratorId { get; init; }

    /// <summary>Decision, Proposal, Confirmation, Overruled or System.</summary>
    public string? Kind { get; init; }

    public string? Outcome { get; init; }

    public string? ReasonCode { get; init; }

    public required string ContentType { get; init; }

    public required string ContentId { get; init; }

    public required string ActionType { get; init; }

    public string? Reason { get; init; }

    public required string PreviousStatus { get; init; }

    public required string NewStatus { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Acknowledges a recorded community confirmation.</summary>
public sealed record ConfirmationResponse
{
    public required Guid ConfirmationId { get; init; }

    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    /// <summary>
    /// Always zero. Kept because the response shape is frozen; this route no longer scores,
    /// because it cannot verify whose work it is being told about. Confirmations are paid from
    /// corroborating reactions instead.
    /// </summary>
    public required int PointsAwarded { get; init; }

    public required DateTimeOffset ConfirmedAt { get; init; }
}

/// <summary>
/// How many members have confirmed one thing, and whether the caller is one of them.
/// <para>
/// The read side of <c>POST /trust/contributions/confirmations</c>. The write side has existed
/// since the module shipped and nothing could ever read it back —
/// <c>CountForTargetAsync</c> was written and never called — so every surface that offered a
/// confirm control had to either invent the count or show a number from somewhere else.
/// </para>
/// <para>
/// <b>There is deliberately no percentage here.</b> A share of readers needs a count of readers,
/// and this module records no impressions — a rate computed from confirmations alone would have
/// its denominator invented, which is the defect this route exists to remove rather than move.
/// </para>
/// </summary>
public sealed record ConfirmationSummaryResponse
{
    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    /// <summary>Distinct members who confirmed. One per member, enforced by a unique index.</summary>
    public required int ConfirmationCount { get; init; }

    /// <summary>Whether the caller already confirmed this, so the control renders as spent.</summary>
    public required bool ConfirmedByViewer { get; init; }
}

/// <summary>
/// A moderation decision about a member's own content, as they read it (7 Nov 2026): what happened,
/// why, the note Diaspora wrote, and what they can do. Never the moderator, never the reporters.
/// </summary>
public sealed record MyModerationCaseResponse
{
    public required Guid CaseId { get; init; }

    public required string ContentType { get; init; }

    public required string ContentId { get; init; }

    public string? Title { get; init; }

    /// <summary>HidePendingReview, Remove, Restore or NoAction. Null while nothing has been decided.</summary>
    public string? Outcome { get; init; }

    public string? ReasonCode { get; init; }

    public string? ReasonLabel { get; init; }

    /// <summary>The note of the decision that is in force.</summary>
    public string? Note { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>Hidden by the system after many reports; no person has decided yet.</summary>
    public bool HiddenAutomatically { get; init; }

    /// <summary>A removal waiting for a second member of staff.</summary>
    public bool RemovalPending { get; init; }

    public DateTimeOffset? AppealDeadline { get; init; }

    public bool CanRequestReview { get; init; }

    public DateTimeOffset? ReviewRequestedAt { get; init; }
}
