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

    /// <summary>
    /// What it was before, or null when there was no before — the request itself.
    /// <para>
    /// Nullable since BSEC-007 (10 Nov 2026). It was a non-nullable status, and a created
    /// withdrawal had nothing honest to put in it, which is one of the reasons creation raised no
    /// event at all for a year. "Requested, from nothing" is a real transition and this is how it
    /// says so.
    /// </para>
    /// </summary>
    public WithdrawalStatus? PreviousStatus { get; init; }

    public required WithdrawalStatus Status { get; init; }

    public required long GrossMinorUnits { get; init; }

    public required long FeeMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>The masked label. Never the credential behind it.</summary>
    public string? DestinationLabel { get; init; }

    public string? FailureCode { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
