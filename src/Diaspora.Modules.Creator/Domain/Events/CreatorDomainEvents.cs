using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Events;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The Creator Platform's domain events.
//
// Private to this module, carrying its own identifiers and enums. Each is settled by exactly one
// handler here — DSP1002 reports a second — and the relay is what decides which of them becomes a
// published fact other modules may consume.
//
// Eight events for eleven gaps, and the ratio is the point. §23 of the phase brief: "Do not publish
// events merely because an endpoint exists. Only meaningful domain state transitions should emit
// events." Reading a leaderboard, listing courses and saving a course raise nothing.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The served creator status moved.
/// <para>
/// Raised only when the status a member can see actually changed — not when the earned rung moved
/// underneath an override, and not on every recalculation. See <c>CreatorProfile.ApplyStatus</c>.
/// </para>
/// </summary>
public sealed record CreatorStatusChangedDomainEvent(
    CreatorId CreatorId,
    Guid UserId,
    CreatorStatus PreviousStatus,
    CreatorStatus CurrentStatus,
    DateTimeOffset ChangedAt) : DomainEvent;

/// <summary>
/// A creator-programme application reached a decided state.
/// <para>
/// Raised on approval, refusal and suspension, and on an appeal's outcome. Not raised when the
/// member saves a draft or a reviewer picks the application up — the member is watching those.
/// </para>
/// </summary>
public sealed record CreatorVerificationDecidedDomainEvent(
    CreatorVerificationApplicationId ApplicationId,
    CreatorId CreatorId,
    Guid UserId,
    CreatorVerificationState PreviousState,
    CreatorVerificationState CurrentState,
    string? Reason,
    DateTimeOffset DecidedAt) : DomainEvent;

/// <summary>
/// A milestone crossed its target.
/// <para>
/// <b>Raised on completion, once, and never on a claim.</b> The register asks for
/// <c>creator.milestone.completed</c> and the phase brief adds: "The event must be emitted only
/// once according to the platform's event semantics." Completion is a fact about the world that
/// happens once; claiming is the member acting on it, and a repeated claim must never produce
/// another anything.
/// </para>
/// </summary>
public sealed record CreatorMilestoneCompletedDomainEvent(
    CreatorMilestoneProgressId ProgressId,
    CreatorMilestoneId MilestoneId,
    CreatorId CreatorId,
    Guid UserId,
    string MilestoneKey,
    DateTimeOffset CompletedAt) : DomainEvent;

/// <summary>A creator's progress on a challenge moved. Published so the client can invalidate.</summary>
public sealed record ChallengeProgressChangedDomainEvent(
    CreatorChallengeEnrollmentId EnrollmentId,
    CreatorChallengeId ChallengeId,
    CreatorId CreatorId,
    Guid UserId,
    long Current,
    long Target,
    int Percentage,
    DateTimeOffset ChangedAt) : DomainEvent;

/// <summary>Every task on a challenge is done. Raised once, on the transition.</summary>
public sealed record ChallengeCompletedDomainEvent(
    CreatorChallengeEnrollmentId EnrollmentId,
    CreatorChallengeId ChallengeId,
    CreatorId CreatorId,
    Guid UserId,
    DateTimeOffset CompletedAt) : DomainEvent;

/// <summary>
/// A grant application was decided.
/// <para>
/// Carries the awarded amount because the funding leg needs it — and carries nothing of the
/// applicant's answers, because a downstream consumer processing a payment has no business reading
/// what somebody wrote about their community project. §10 of the phase brief: "with enough
/// information for downstream financial processing without leaking unnecessary applicant data."
/// </para>
/// </summary>
public sealed record GrantApplicationDecidedDomainEvent(
    GrantApplicationId ApplicationId,
    CommunityGrantId GrantId,
    CreatorId CreatorId,
    Guid ApplicantUserId,
    GrantApplicationState PreviousState,
    GrantApplicationState CurrentState,
    long? AwardedMinorUnits,
    string? Currency,
    string? Reason,
    DateTimeOffset DecidedAt) : DomainEvent;

/// <summary>
/// A referral satisfied the qualification rule.
/// <para>
/// <b>Not raised on sign-up.</b> The register is emphatic that the two are different facts, and the
/// reward is owed on this one alone.
/// </para>
/// </summary>
public sealed record ReferralQualifiedDomainEvent(
    CreatorReferralId ReferralId,
    CreatorId ReferrerCreatorId,
    Guid ReferrerUserId,
    Guid InviteeUserId,
    int RewardPoints,
    DateTimeOffset QualifiedAt) : DomainEvent;

/// <summary>The sign-up bonus was awarded. The points grant rides on this.</summary>
public sealed record SignupBonusAwardedDomainEvent(
    SignupBonusId BonusId,
    Guid UserId,
    int Points,
    DateTimeOffset AwardedAt) : DomainEvent;

/// <summary>
/// Points were earned for something the programme recognises.
/// <para>
/// <b>The one place this module ever awards points, and it awards none itself.</b> The settling
/// handler translates this into B15's <c>RewardPointsGrantedIntegrationEvent</c>; B15 holds the
/// only balance on the platform. <see cref="GrantReference"/> is the anti-double-award barrier and
/// is this module's own handle for the thing being rewarded — a milestone claim, a challenge claim,
/// a qualified referral, a sign-up bonus.
/// </para>
/// </summary>
public sealed record CreatorPointsEarnedDomainEvent(
    CreatorId CreatorId,
    Guid UserId,
    long Points,
    string Source,
    string GrantReference,
    DateTimeOffset EarnedAt,
    string? PolicyVersion = null) : DomainEvent;

/// <summary>A course was finished, once per member and course. Pays no points (Q6).</summary>
public sealed record CourseCompletedDomainEvent(
    CreatorId CreatorId,
    Guid UserId,
    AcademyCourseId CourseId,
    string CourseSlug,
    DateTimeOffset CompletedAt) : DomainEvent;
