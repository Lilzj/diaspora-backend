using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain.Events;

/// <summary>Raised when a member's trust level moves, in either direction.</summary>
public sealed record TrustLevelChangedDomainEvent(
    TrustProfileId ProfileId,
    UserId UserId,
    TrustTier From,
    TrustTier To,
    string Reason,
    DateTimeOffset At) : DomainEvent;

/// <summary>Raised when a badge is earned.</summary>
public sealed record BadgeAwardedDomainEvent(
    TrustProfileId ProfileId,
    UserId UserId,
    BadgeType Badge,
    string? CountryCode,
    DateTimeOffset At) : DomainEvent;

/// <summary>Raised when a badge is taken back — a revoked verification, or a violation.</summary>
public sealed record BadgeRevokedDomainEvent(
    TrustProfileId ProfileId,
    UserId UserId,
    BadgeType Badge,
    string? CountryCode,
    string Reason,
    DateTimeOffset At) : DomainEvent;

/// <summary>Raised when account health changes, which gates publishing.</summary>
public sealed record AccountHealthChangedDomainEvent(
    TrustProfileId ProfileId,
    UserId UserId,
    AccountHealth From,
    AccountHealth To,
    DateTimeOffset At) : DomainEvent;

/// <summary>
/// Raised when a reassessment moves the member's risk level, in either direction (RWD-14, CS-4b).
/// A reassessment at the same level renews the assessment and raises nothing.
/// </summary>
public sealed record MemberRiskLevelChangedDomainEvent(
    TrustProfileId ProfileId,
    UserId UserId,
    RiskLevel From,
    RiskLevel To,
    DateTimeOffset At) : DomainEvent;

/// <summary>Raised when a member crosses a reputation level boundary.</summary>
public sealed record ReputationLevelChangedDomainEvent(
    ReputationProfileId ProfileId,
    UserId UserId,
    ReputationLevel From,
    ReputationLevel To,
    int CombinedScore,
    DateTimeOffset At) : DomainEvent;

/// <summary>Raised for every scoring event, so the timeline and any listener can follow along.</summary>
public sealed record ReputationScoredDomainEvent(
    ReputationProfileId ProfileId,
    UserId UserId,
    ReputationDimension Dimension,
    string ActionType,
    int Points,
    DateTimeOffset At) : DomainEvent;

/// <summary>Raised when a member becomes eligible to publish guides.</summary>
public sealed record ContributorApprovedDomainEvent(
    UserId UserId,
    string? CountryCode,
    DateTimeOffset At) : DomainEvent;

/// <summary>Raised when a member becomes a community guide.</summary>
public sealed record GuideApprovedDomainEvent(
    UserId UserId,
    string? CountryCode,
    DateTimeOffset At) : DomainEvent;

/// <summary>Raised when an organisation's verification is granted.</summary>
public sealed record OrganizationVerifiedDomainEvent(
    UserId UserId,
    VerificationRequestId RequestId,
    DateTimeOffset At) : DomainEvent;

public sealed record AppealSubmittedDomainEvent(
    AppealId AppealId,
    UserId UserId,
    AppealType Type,
    string TargetId,
    DateTimeOffset At) : DomainEvent;

public sealed record AppealApprovedDomainEvent(
    AppealId AppealId,
    UserId UserId,
    AppealType Type,
    string TargetId,
    string? Explanation,
    DateTimeOffset At,
    UserId? DecidedBy = null) : DomainEvent;

public sealed record AppealRejectedDomainEvent(
    AppealId AppealId,
    UserId UserId,
    AppealType Type,
    string TargetId,
    string? Explanation,
    DateTimeOffset At) : DomainEvent;

public sealed record ModerationDecisionRecordedDomainEvent(
    ModerationItemId ItemId,
    UserId ModeratorId,
    ModerationContentType ContentType,
    string ContentId,
    ModerationActionType Action,
    DateTimeOffset At) : DomainEvent;
