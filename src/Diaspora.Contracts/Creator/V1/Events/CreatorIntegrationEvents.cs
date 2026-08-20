using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Creator.V1.Events;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The Creator Platform's published contracts.
//
// Eight events for eleven gaps, and the ratio is deliberate. §23 of the Wave 6 brief: "Do not publish
// events merely because an endpoint exists. Only meaningful domain state transitions should emit
// events." Reading a leaderboard, listing courses, saving a course and editing a profile publish
// nothing.
//
// Every one of these is what B13 needs to notify on and what B19 will need to count. None of them
// carries anything private: no evidence, no grant answers, no referral details, no risk signal and
// no points balance. The register's own list of creator notification templates — CREATOR-GAP-015,
// which belongs to B13.2 — maps onto these one for one, and that mapping is the reason the payloads
// carry the identifiers a deep link needs.
//
// Flat primitives throughout, so a consumer parses one without referencing the Creator module.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A creator's served status changed.
/// <para>
/// Raised only when the status a member can see actually moved — not when the earned rung moved
/// underneath an override, and not on every recalculation. CREATOR-GAP-001 asks for this so "B13 can
/// notify on promotion"; a demotion is the same event with the two statuses the other way round, and
/// what to do about that is B13's decision rather than this module's.
/// </para>
/// </summary>
public sealed record CreatorStatusChangedIntegrationEvent : IntegrationEvent
{
    public required Guid CreatorId { get; init; }

    public required Guid MemberUserId { get; init; }

    public required string PreviousStatus { get; init; }

    public required string CurrentStatus { get; init; }

    /// <summary>Which version of the ladder decided it. A changed rule is visible rather than silent.</summary>
    public required string PolicyVersion { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>
/// A creator-programme application was decided.
/// <para>
/// <b>Not an identity verification.</b> Trust publishes its own contracts for that; this one says
/// only whether somebody qualified for the creator programme.
/// </para>
/// </summary>
public sealed record CreatorVerificationDecidedIntegrationEvent : IntegrationEvent
{
    public required Guid ApplicationId { get; init; }

    public required Guid CreatorId { get; init; }

    public required Guid MemberUserId { get; init; }

    public required string PreviousState { get; init; }

    public required string CurrentState { get; init; }

    /// <summary>The reviewer's words, where they gave any. Never an internal note.</summary>
    public string? Reason { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}

/// <summary>
/// A milestone was completed.
/// <para>
/// <b>Completion, not a claim.</b> Completion happens once and is a fact about the world; claiming is
/// the member acting on it, and a repeated claim produces nothing at all.
/// </para>
/// </summary>
public sealed record CreatorMilestoneCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid MilestoneId { get; init; }

    public required Guid CreatorId { get; init; }

    public required Guid MemberUserId { get; init; }

    /// <summary>The milestone's stable key — <c>first-ten-guides</c>. What a template switches on.</summary>
    public required string MilestoneKey { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}

/// <summary>
/// A creator's progress on a challenge moved.
/// <para>
/// Carries the figures rather than a percentage alone, so a consumer can render "3 of 5" without
/// asking. Published on every move, which is why a consumer that notifies should throttle rather
/// than send one message per guide.
/// </para>
/// </summary>
public sealed record CreatorChallengeProgressChangedIntegrationEvent : IntegrationEvent
{
    public required Guid ChallengeId { get; init; }

    public required Guid CreatorId { get; init; }

    public required Guid MemberUserId { get; init; }

    public required long Current { get; init; }

    public required long Target { get; init; }

    public required int Percentage { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>Every task on a challenge is done. Raised once, on the transition.</summary>
public sealed record CreatorChallengeCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid ChallengeId { get; init; }

    public required Guid CreatorId { get; init; }

    public required Guid MemberUserId { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}

/// <summary>
/// A grant application was decided.
/// <para>
/// <b>Carries the awarded amount and none of the answers.</b> §10 of the Wave 6 brief: "with enough
/// information for downstream financial processing without leaking unnecessary applicant data." A
/// consumer executing a payment has no business reading what somebody wrote about their project.
/// </para>
/// <para>
/// <b>The funding leg is B15's.</b> This event is the trigger and the record; the money moves through
/// the Financial Platform's own credit or payout path, never through the Creator Platform.
/// </para>
/// </summary>
public sealed record GrantApplicationDecidedIntegrationEvent : IntegrationEvent
{
    public required Guid ApplicationId { get; init; }

    public required Guid GrantId { get; init; }

    public required Guid CreatorId { get; init; }

    public required Guid ApplicantUserId { get; init; }

    public required string PreviousState { get; init; }

    public required string CurrentState { get; init; }

    /// <summary>What was awarded, in minor units. Null on anything but an approval.</summary>
    public long? AwardedMinorUnits { get; init; }

    public string? Currency { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}

/// <summary>
/// A referral satisfied the qualification rule.
/// <para>
/// <b>Not raised on sign-up</b>, which is the whole of CREATOR-GAP-009: a signup and a qualified
/// referral are different facts and only the second is owed a reward.
/// </para>
/// <para>
/// The invitee's identifier is carried because a consumer may need to attribute; nothing about who
/// they are is.
/// </para>
/// </summary>
public sealed record CreatorReferralQualifiedIntegrationEvent : IntegrationEvent
{
    public required Guid ReferralId { get; init; }

    public required Guid ReferrerCreatorId { get; init; }

    public required Guid ReferrerUserId { get; init; }

    public required Guid InviteeUserId { get; init; }

    public required int RewardPoints { get; init; }

    /// <summary>Which version of the qualification rule decided it.</summary>
    public required string QualificationVersion { get; init; }

    public required DateTimeOffset QualifiedAt { get; init; }
}

/// <summary>
/// A sign-up bonus was awarded.
/// <para>
/// <b>Carries the points and no balance.</b> What the member now holds is B15's answer, and a figure
/// here would be a second one that could disagree.
/// </para>
/// </summary>
public sealed record SignupBonusAwardedIntegrationEvent : IntegrationEvent
{
    public required Guid MemberUserId { get; init; }

    public required int Points { get; init; }

    public required DateTimeOffset AwardedAt { get; init; }
}
