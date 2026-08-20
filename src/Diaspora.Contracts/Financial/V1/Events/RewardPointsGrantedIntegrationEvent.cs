using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A member earned points. Points are not money.
/// <para>
/// <b>This event carries no currency and no amount, and the omission is the design.</b> A reward
/// point is a count of something the platform decided to recognise; what it is worth is a policy the
/// Financial Platform owns and re-reads at conversion time. Putting a monetary figure here would fix
/// a rate at the moment of earning, and a member who earned a thousand points last year would be owed
/// last year's rate for ever.
/// </para>
/// <para>
/// The reputation engine, a creator platform or an operator tool grants points by publishing the
/// fact; nothing outside the Financial Platform ever converts them.
/// </para>
/// </summary>
public sealed record RewardPointsGrantedIntegrationEvent : IntegrationEvent
{
    public required Guid MemberUserId { get; init; }

    /// <summary>How many points. Whole, never fractional, and never negative.</summary>
    public required long Points { get; init; }

    /// <summary>Why they were earned — <c>ReferralQualified</c>, <c>GuidePublished</c>, and so on.</summary>
    public required string Source { get; init; }

    /// <summary>
    /// The grantor's own idempotency handle. Two publishes carrying one key award points once —
    /// which is the whole of double-award prevention, and it lives with the grantor because only the
    /// grantor knows whether two events describe one earning.
    /// </summary>
    public required string GrantReference { get; init; }

    public required DateTimeOffset GrantedAt { get; init; }
}
