using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A wallet was activated, frozen or closed.
/// <para>
/// One contract for every transition, carrying both the old and the new status. A consumer
/// maintaining a view needs the freeze at least as much as the activation, and the platform has
/// twice found that a contract covering only the happy direction leaves half a state machine
/// unobservable.
/// </para>
/// </summary>
public sealed record WalletStatusChangedIntegrationEvent : IntegrationEvent
{
    public required Guid WalletId { get; init; }

    public required Guid OwnerId { get; init; }

    public required string PreviousStatus { get; init; }

    /// <summary><c>Pending</c>, <c>Active</c>, <c>Frozen</c> or <c>Closed</c>.</summary>
    public required string Status { get; init; }

    public required string Currency { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// What <see cref="OwnerId"/> identifies — <c>Personal</c>, <c>Organization</c>,
    /// <c>Provider</c>, <c>Business</c> or <c>Platform</c>.
    /// <para>
    /// <b>Appended by PSR-1.1, because without it <see cref="OwnerId"/> is unusable by any consumer
    /// that wants to address a person.</b> A wallet is owned by a member, a community organization, a
    /// provider, a business or the platform itself, and four of those five are not user identifiers.
    /// A consumer that assumed otherwise would send "your wallet was frozen" to an organization id —
    /// which resolves to nobody, silently, exactly the failure mode B13.1 was created to end.
    /// </para>
    /// <para>
    /// Nullable and appended last, per B13.1's procedure: an event serialized before this phase
    /// deserializes with the property absent, and a consumer that cannot tell what the owner is must
    /// treat that as "not a person" rather than guess.
    /// </para>
    /// </summary>
    public string? OwnerType { get; init; }
}
