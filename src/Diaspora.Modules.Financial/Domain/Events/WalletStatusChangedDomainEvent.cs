using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A wallet became usable, was frozen, or was closed.
/// <para>
/// One event for every transition rather than one per direction, carrying both the old and the new
/// status. A consumer maintaining a view needs the freeze at least as much as the activation, and
/// the platform has twice found that a contract covering only the happy direction leaves the other
/// half of the state machine unobservable.
/// </para>
/// </summary>
public sealed record WalletStatusChangedDomainEvent : DomainEvent
{
    public required WalletId WalletId { get; init; }

    public required FinancialAccountId AccountId { get; init; }

    public required Guid OwnerId { get; init; }

    /// <summary>
    /// What <see cref="OwnerId"/> identifies. Carried so the relay can publish it without loading the
    /// wallet again — a wallet is owned by a member, an organization, a provider, a business or the
    /// platform, and only the first of those is somebody who can be told anything.
    /// </summary>
    public required FinancialAccountType OwnerType { get; init; }

    public required WalletStatus PreviousStatus { get; init; }

    public required WalletStatus Status { get; init; }

    public required string Currency { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
