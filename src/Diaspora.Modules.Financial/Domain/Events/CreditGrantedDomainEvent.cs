using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>Platform credit was granted to a wallet.</summary>
public sealed record CreditGrantedDomainEvent : DomainEvent
{
    public required CreditGrantId GrantId { get; init; }

    public required WalletId WalletId { get; init; }

    public required Guid OwnerId { get; init; }

    /// <summary>
    /// What <see cref="OwnerId"/> identifies.
    /// <para>
    /// Stamped at raise time from the wallet the command handler already holds, rather than stored
    /// on the aggregate — the same procedure that closed DEBT-006. A grant's owner type never
    /// changes after issue, so persisting it would be a column that can only ever restate what the
    /// wallet says.
    /// </para>
    /// </summary>
    public required FinancialAccountType OwnerType { get; init; }

    public required CreditKind Kind { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required string Reason { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset GrantedAt { get; init; }
}
