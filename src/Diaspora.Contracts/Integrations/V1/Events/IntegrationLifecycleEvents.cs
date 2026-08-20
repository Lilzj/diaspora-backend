using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Integrations.V1.Events;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// What the Integration Foundation tells the rest of the platform.
//
// These five contracts are the thing B13.3 said FRONTEND-PHASE16-GAP-017 was waiting for: "Grant
// and sync lifecycle events." Nothing about B13 needed changing — its category, template and rule
// contributor seams were already public — so the notification half of that gap is a catalogue this
// module contributes and these five facts to hang it on.
//
// ── What is deliberately absent from every one of them ──────────────────────────────────────────
//
// No token. No authorization code. No calendar title. No calendar identifier. No external account
// identifier. An integration event is written to the outbox, serialised, retried, dead-lettered and
// read by operators, and it reaches consumers this module does not know about. A calendar's title
// is personal — "Therapy", "Dad's chemo" — and an account label is a member's email address; both
// belong in one table with one access rule, not on a message bus.
//
// What travels is: who it concerns, which provider, and what happened. Everything a consumer needs
// to compose a notification, and nothing that would be a disclosure if a dead-letter queue were
// read by somebody doing operations rather than support.
//
// ── UserId, and why it is on all five ───────────────────────────────────────────────────────────
//
// B13.1's rule: a contract must carry the recipient it concerns, because a consumer that has to
// resolve one is a consumer that eventually resolves the wrong one. Every connection on this
// platform is owned by exactly one subject today, so the field is a plain Guid rather than a list.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A member connected an external account.
/// <para>
/// Also the security notification the register singles out: "'New sign-in method linked' is a
/// security notification and must be in B13's mandatory set: a member must not be able to switch
/// off the alert that tells them somebody attached an account to theirs." The mandatory routing is
/// applied by the category this module contributes, and the body carries no address — "the body must
/// not contain the linked address in a lock-screen preview."
/// </para>
/// </summary>
public sealed record IntegrationConnectedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    /// <summary>The dotted provider key — <c>calendar.google</c>. Never a vendor's own account id.</summary>
    public required string Provider { get; init; }

    /// <summary>The domain half — <c>calendar</c>, <c>identity</c>. What a template branches on.</summary>
    public required string ProviderDomain { get; init; }

    /// <summary>
    /// Whether this is a first connection rather than a re-authorization.
    /// <para>
    /// The two need different wording and the same event: "you connected Google Calendar" and "you
    /// reconnected Google Calendar" are not the same sentence, and two contracts for one fact would
    /// have meant two rules and two dedup windows over one member action.
    /// </para>
    /// </summary>
    public required bool IsFirstConnection { get; init; }

    /// <summary>Scope names granted. Names only; no token, and no scope value that carries data.</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    public required DateTimeOffset ConnectedAt { get; init; }
}

/// <summary>A member disconnected an external account, or a provider signal said the grant is gone.</summary>
public sealed record IntegrationDisconnectedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Provider { get; init; }

    public required string ProviderDomain { get; init; }

    /// <summary>
    /// Whether the provider confirmed the revocation.
    /// <para>
    /// Published rather than assumed, because a disconnection during a provider outage leaves the
    /// grant live at the provider and that is a fact an audit trail needs.
    /// </para>
    /// </summary>
    public required bool RevokedAtProvider { get; init; }

    /// <summary>
    /// <c>member</c> or <c>provider</c>. Who initiated it.
    /// <para>
    /// A member who disconnected does not need telling; a member whose grant was removed at the
    /// provider does. The rule this module contributes to B13 branches on exactly this field.
    /// </para>
    /// </summary>
    public required string Initiator { get; init; }

    public required DateTimeOffset DisconnectedAt { get; init; }
}

/// <summary>
/// A grant is about to lapse and nobody has been told.
/// <para>
/// Announced once per grant per expiry by the maintenance sweep. Announcing on every pass would be a
/// notification every fifteen minutes for three days, which is how a member switches the whole
/// category off.
/// </para>
/// </summary>
public sealed record IntegrationGrantExpiringIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Provider { get; init; }

    public required string ProviderDomain { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// A grant stopped working and only the member can fix it.
/// <para>
/// Distinct from <see cref="IntegrationGrantExpiringIntegrationEvent"/> because the wording and the
/// urgency differ: one is "this will stop working", the other is "this has stopped working", and a
/// member who receives the second having never seen the first should not be told it is a warning.
/// </para>
/// </summary>
public sealed record IntegrationReauthorizationRequiredIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Provider { get; init; }

    public required string ProviderDomain { get; init; }

    /// <summary>
    /// <c>expired</c>, <c>revoked</c> or <c>rejected</c>. A category, never a provider payload.
    /// </summary>
    public required string Reason { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }
}

/// <summary>
/// A synchronisation ended other than cleanly.
/// <para>
/// Counts rather than contents. "Eleven of twelve entries were written" is what a member can act on;
/// which eleven is a list of their calendar entries and does not belong on a bus.
/// </para>
/// </summary>
public sealed record IntegrationSyncFailedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Provider { get; init; }

    public required string ProviderDomain { get; init; }

    /// <summary><c>partial</c> or <c>failed</c>.</summary>
    public required string Phase { get; init; }

    public required int ItemsSynced { get; init; }

    public required int ItemsFailed { get; init; }

    /// <summary>True when the member has to do something rather than wait.</summary>
    public required bool NeedsAttention { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
