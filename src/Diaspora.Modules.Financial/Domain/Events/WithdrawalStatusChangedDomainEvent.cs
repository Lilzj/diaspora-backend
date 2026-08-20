using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>A payout request moved from one state to another.</summary>
public sealed record WithdrawalStatusChangedDomainEvent : DomainEvent
{
    public required WithdrawalId WithdrawalId { get; init; }

    public required Guid RequestedByUserId { get; init; }

    public required WalletId WalletId { get; init; }

    public required WithdrawalStatus PreviousStatus { get; init; }

    public required WithdrawalStatus Status { get; init; }

    public required long GrossMinorUnits { get; init; }

    public required long FeeMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>The masked label. Never the credential behind it.</summary>
    public string? DestinationLabel { get; init; }

    public string? FailureCode { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
