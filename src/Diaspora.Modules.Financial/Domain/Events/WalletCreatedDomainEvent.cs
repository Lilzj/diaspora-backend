using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>A wallet now exists. Raised once, at creation, before it is usable.</summary>
public sealed record WalletCreatedDomainEvent : DomainEvent
{
    public required WalletId WalletId { get; init; }

    public required FinancialAccountId AccountId { get; init; }

    public required Guid OwnerId { get; init; }

    public required FinancialAccountType OwnerType { get; init; }

    public required WalletType WalletType { get; init; }

    public required string Currency { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
