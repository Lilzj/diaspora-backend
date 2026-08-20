using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Trust.V1.Events;

/// <summary>
/// The Trust &amp; Verification Engine's published events.
/// <para>
/// They live in <c>Diaspora.Contracts</c> rather than inside the Trust module for the same
/// reason Identity's do: a consumer must be able to parse the event without referencing the
/// module that raised it. A Jobs module reacting to <see cref="TrustLevelChangedIntegrationEvent"/>
/// takes a dependency on this assembly and on nothing of Trust's.
/// </para>
/// <para>
/// <b>Versioning.</b> The <c>V1</c> namespace segment is the version. These records are
/// additive-only: a new optional property is a compatible change, anything else is a
/// <c>V2</c> type published alongside V1 until consumers migrate. Renaming a property here
/// silently breaks every deserialiser in the estate, which is exactly the failure a published
/// contract exists to prevent.
/// </para>
/// <para>
/// <b>Outbox readiness.</b> Every event carries its own <c>EventId</c> and <c>OccurredAt</c>
/// from <see cref="IntegrationEvent"/>, and every payload is a flat, serialisable record with
/// no domain types — so the same instance that is dispatched in-process today can be written
/// to an outbox table and relayed later without changing shape. Consumers must treat
/// <c>EventId</c> as the idempotency key: at-least-once delivery is the assumption.
/// </para>
/// </summary>
public sealed record VerificationSubmittedIntegrationEvent : IntegrationEvent
{
    public required Guid VerificationId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>The <c>VerificationType</c> name.</summary>
    public required string VerificationType { get; init; }

    public string? Nationality { get; init; }

    public string? ResidenceCountry { get; init; }

    public string? TargetCountry { get; init; }

    public required DateTimeOffset SubmittedAt { get; init; }
}

/// <summary>
/// A verification was granted.
/// <para>
/// The event that ultimately reaches Identity as a <c>Granted</c> trust decision. Note it
/// does not itself change anyone's access state — per ADR-002 that happens only through
/// <c>POST /api/v1/trust/decisions</c>, and this event is the trigger for making that call,
/// not a substitute for it.
/// </para>
/// </summary>
public sealed record VerificationApprovedIntegrationEvent : IntegrationEvent
{
    public required Guid VerificationId { get; init; }

    public required Guid UserId { get; init; }

    public required string VerificationType { get; init; }

    public string? TargetCountry { get; init; }

    /// <summary>True when only part of the claim was granted.</summary>
    public required bool Partial { get; init; }

    /// <summary>When the grant lapses. Null means it does not.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset ApprovedAt { get; init; }
}

public sealed record VerificationRejectedIntegrationEvent : IntegrationEvent
{
    public required Guid VerificationId { get; init; }

    public required Guid UserId { get; init; }

    public required string VerificationType { get; init; }

    /// <summary>
    /// The country community the refused claim was about, for a <c>Country</c> verification.
    /// Null for every other type, and null on any payload serialised before B3.0.1.
    /// <para>
    /// Added in <b>B3.0.1</b>, additive and optional. Without it a consumer holding a
    /// per-country residency projection could not attribute a refusal to a country, and the
    /// only alternative — inferring from the member's most recent submission — is wrong the
    /// first time someone has two in flight, and wrong invisibly. See the
    /// <c>docs/trust/contract-evolution-b3.0.1.md</c> report.
    /// </para>
    /// </summary>
    public string? TargetCountry { get; init; }

    /// <summary>SCREAMING_SNAKE reason code.</summary>
    public required string ReasonCode { get; init; }

    public string? Details { get; init; }

    public required DateTimeOffset RejectedAt { get; init; }
}

/// <summary>A granted verification lapsed because its evidence expired.</summary>
public sealed record VerificationExpiredIntegrationEvent : IntegrationEvent
{
    public required Guid VerificationId { get; init; }

    public required Guid UserId { get; init; }

    public required string VerificationType { get; init; }

    /// <summary>
    /// The country community the lapsed grant was about. Null for every other type, and null
    /// on any payload serialised before B3.0.1.
    /// <para>
    /// Added in <b>B3.0.1</b> for consistency with the rest of the verification lifecycle. A
    /// consumer could already attribute a lapse by matching
    /// <see cref="VerificationId"/> against the approval it recorded earlier, but that only
    /// works if it saw the approval — which a consumer that joined the platform later, or
    /// rebuilt its projection from a partial replay, did not.
    /// </para>
    /// </summary>
    public string? TargetCountry { get; init; }

    public required DateTimeOffset ExpiredAt { get; init; }
}

/// <summary>A granted verification was withdrawn for cause.</summary>
public sealed record VerificationRevokedIntegrationEvent : IntegrationEvent
{
    public required Guid VerificationId { get; init; }

    public required Guid UserId { get; init; }

    public required string VerificationType { get; init; }

    /// <summary>
    /// The country community the withdrawn grant was about. Null for every other type, and
    /// null on any payload serialised before B3.0.1.
    /// <para>
    /// Added in <b>B3.0.1</b>, for the same reason as on
    /// <see cref="VerificationExpiredIntegrationEvent"/>: identifier-matching works only for a
    /// consumer that saw the approval.
    /// </para>
    /// </summary>
    public string? TargetCountry { get; init; }

    public required string Reason { get; init; }

    public required DateTimeOffset RevokedAt { get; init; }
}

public sealed record AppealSubmittedIntegrationEvent : IntegrationEvent
{
    public required Guid AppealId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>Verification, Content, Suspension, Publishing.</summary>
    public required string AppealType { get; init; }

    public required string TargetId { get; init; }

    public required DateTimeOffset SubmittedAt { get; init; }
}

public sealed record AppealApprovedIntegrationEvent : IntegrationEvent
{
    public required Guid AppealId { get; init; }

    public required Guid UserId { get; init; }

    public required string AppealType { get; init; }

    public required string TargetId { get; init; }

    public string? Explanation { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}

public sealed record AppealRejectedIntegrationEvent : IntegrationEvent
{
    public required Guid AppealId { get; init; }

    public required Guid UserId { get; init; }

    public required string AppealType { get; init; }

    public required string TargetId { get; init; }

    public string? Explanation { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}

/// <summary>
/// A member's trust tier moved, in either direction.
/// <para>
/// The single most widely consumed Trust event: any module gating a capability on trust
/// should invalidate its cached answer when this arrives.
/// </para>
/// </summary>
public sealed record TrustLevelChangedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    /// <summary>The module's own tier name — Untrusted, Basic, Provisional, Verified, Trusted, Steward.</summary>
    public required string PreviousLevel { get; init; }

    public required string CurrentLevel { get; init; }

    /// <summary>
    /// The coarse <c>Platform.TrustLevel</c> the tier projects onto — None, Basic, Verified,
    /// Premium. Carried alongside so a consumer that only cares about the four-value ladder
    /// need not know the module's finer one.
    /// </summary>
    public required string PlatformTrustLevel { get; init; }

    public required string Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

public sealed record BadgeAwardedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Badge { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset AwardedAt { get; init; }
}

public sealed record BadgeRevokedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Badge { get; init; }

    public string? CountryCode { get; init; }

    public required string Reason { get; init; }

    public required DateTimeOffset RevokedAt { get; init; }
}

/// <summary>A member became eligible to publish guides.</summary>
public sealed record ContributorApprovedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset ApprovedAt { get; init; }
}

/// <summary>A member joined the community guide programme.</summary>
public sealed record GuideApprovedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset ApprovedAt { get; init; }
}

/// <summary>An organisation's verification was granted.</summary>
public sealed record OrganizationVerifiedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required Guid VerificationId { get; init; }

    /// <summary>
    /// The country the organisation was verified in, when the claim named one.
    /// <para>
    /// Added in <b>B3.0.1</b>. This event, <see cref="ContributorApprovedIntegrationEvent"/>
    /// and <see cref="GuideApprovedIntegrationEvent"/> are siblings raised from one approval,
    /// and the other two already carried <c>CountryCode</c>. A consumer switching on the three
    /// had to special-case this one for no reason a reader could reconstruct.
    /// </para>
    /// </summary>
    public string? CountryCode { get; init; }

    public required DateTimeOffset VerifiedAt { get; init; }
}

/// <summary>
/// A member's reputation level changed.
/// <para>
/// Separate from <see cref="TrustLevelChangedIntegrationEvent"/> because the two ladders move
/// independently: reputation is earned by contributing, trust by proving. A search-ranking
/// consumer wants this one; an authorisation consumer wants the other.
/// </para>
/// </summary>
public sealed record ReputationLevelChangedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string PreviousLevel { get; init; }

    public required string CurrentLevel { get; init; }

    public required int CombinedScore { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>
/// A moderator decided on a piece of content.
/// <para>
/// The event the owning module acts on — Trust holds the queue and the decision, but only
/// the module that published a guide can actually hide it.
/// </para>
/// </summary>
public sealed record ModerationDecisionRecordedIntegrationEvent : IntegrationEvent
{
    public required Guid ItemId { get; init; }

    public required Guid ModeratorId { get; init; }

    /// <summary>Guide, CommunityUpdate, Comment, Listing, Profile, Organization.</summary>
    public required string ContentType { get; init; }

    public required string ContentId { get; init; }

    /// <summary>Approve, Reject, Hide, Archive, Pin, Unpin, Suspend, Restore, Escalate.</summary>
    public required string Action { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}
