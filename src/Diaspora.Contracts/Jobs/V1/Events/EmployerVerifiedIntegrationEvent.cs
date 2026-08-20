using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// An employer's Trust mark changed.
/// <para>
/// <b>The most consequential badge on the platform for this audience.</b> "Verified employer" is
/// what a candidate is actually reading a posting for, on a platform whose members are routinely
/// approached by strangers offering work abroad — which is the exact shape of the most common
/// fraud against them.
/// </para>
/// <para>
/// <b>The revocation direction is the one that must not be missed.</b> A stale badge on a posting
/// from an employer Trust has just revoked is the platform vouching for precisely the wrong
/// party, so every consumer holding a denormalised copy has to act on this — and act on it in
/// the direction of caution first.
/// </para>
/// <para>
/// Named for the brief's <c>EmployerVerified</c> and carries the full transition, because a
/// contract that only fired on approval would leave consumers no way to hear about a withdrawal.
/// </para>
/// </summary>
public sealed record EmployerVerifiedIntegrationEvent : IntegrationEvent
{
    public required Guid EmployerId { get; init; }

    public required string EmployerName { get; init; }

    /// <summary><c>None</c>, <c>Pending</c>, <c>Verified</c>, <c>Revoked</c> or <c>Rejected</c>.</summary>
    public required string PreviousState { get; init; }

    public required string CurrentState { get; init; }

    /// <summary>True only when the employer now holds a mark. The convenience most consumers read.</summary>
    public required bool IsVerified { get; init; }

    /// <summary>Trust's case identifier.</summary>
    public Guid? VerificationId { get; init; }

    /// <summary>The badge label Trust granted. Rendered, never interpreted.</summary>
    public string? Badge { get; init; }

    /// <summary>Why. Shown to the employer, never to candidates.</summary>
    public string? Reason { get; init; }

    /// <summary>When Trust decided, not when the projection was written.</summary>
    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1. The employer's current primary owner.
    /// <para>
    /// <b>Trust decided this, not the employer.</b> A badge granted and a badge revoked are both
    /// facts an employer learns about rather than causes, and the revocation direction is the one
    /// they must not learn about from a candidate. <see cref="Reason"/> is shown to this member
    /// and to nobody else.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}
