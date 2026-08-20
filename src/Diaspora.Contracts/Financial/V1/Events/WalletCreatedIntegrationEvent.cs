using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A wallet now exists for an owner.
/// <para>
/// <b>Carries no balance, on purpose.</b> A wallet is created empty and its balance is derived from
/// the ledger; publishing a number here would be publishing a zero that a consumer might cache and
/// then never correct.
/// </para>
/// <para>
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record WalletCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid WalletId { get; init; }

    public required Guid AccountId { get; init; }

    /// <summary>The identifier in the module that owns the thing — a user, an organization.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary><c>Personal</c>, <c>Organization</c>, <c>Provider</c>, <c>Business</c>.</summary>
    public required string OwnerType { get; init; }

    /// <summary><c>Primary</c>, <c>Escrow</c>, <c>Revenue</c>, <c>Promotional</c>, <c>Clearing</c>.</summary>
    public required string WalletType { get; init; }

    public required string Currency { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
