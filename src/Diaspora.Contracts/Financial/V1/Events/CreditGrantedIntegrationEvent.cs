using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>Platform credit was granted to somebody.</summary>
public sealed record CreditGrantedIntegrationEvent : IntegrationEvent
{
    public required Guid GrantId { get; init; }

    public required Guid OwnerId { get; init; }

    /// <summary>
    /// <c>Promotional</c>, <c>Compensation</c>, <c>Referral</c>, <c>RefundToCredit</c>,
    /// <c>Bonus</c> or <c>Purchased</c>.
    /// </summary>
    public required string Kind { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>Why. Written onto the ledger entry and shown to the member.</summary>
    public required string Reason { get; init; }

    /// <summary>Null when the credit never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset GrantedAt { get; init; }

    /// <summary>
    /// What <see cref="OwnerId"/> identifies — <c>Personal</c>, <c>Organization</c>,
    /// <c>Provider</c>, <c>Business</c> or <c>Platform</c>.
    /// <para>
    /// <b>Appended in PSR-1 Final Certification, closing DEBT-068.</b> This is the same defect
    /// PSR-1.1 fixed on <see cref="WalletStatusChangedIntegrationEvent"/> and deliberately left
    /// here, because carrying it meant changing a domain factory in a phase built specifically
    /// without touching Financial. It was latent rather than live — no consumer addresses this
    /// contract — and a latent defect on a published contract is exactly what a final certification
    /// exists to close before somebody writes the consumer that trips over it.
    /// </para>
    /// <para>
    /// A wallet is owned by a member, a community organization, a provider, a business or the
    /// platform itself, and four of those five are not user identifiers. A consumer that assumed
    /// otherwise would address "you have been given credit" to an organization id, which resolves
    /// to nobody, silently — the failure mode B13.1 was created to end.
    /// </para>
    /// <para>
    /// Nullable and appended last, per B13.1's procedure: an event serialized before this phase
    /// deserializes with the property absent, and a consumer that cannot tell what the owner is must
    /// treat that as "not a person" rather than guess.
    /// </para>
    /// </summary>
    public string? OwnerType { get; init; }
}
