using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Withdrawals;

/// <summary>
/// One request to take money off the platform.
/// <para>
/// <b>The money is held the moment the request is raised, and that is the whole design.</b> A
/// withdrawal that merely recorded an intention would let a member request the same hundred pounds
/// three times and let all three succeed; holding moves the amount from available into reserved
/// through the ledger, so the second request finds an available balance that has already gone down.
/// The concurrency guarantee is not in this type — it is in the wallet's revision token and the
/// posting service's balance check — but the <em>shape</em> that makes those apply is here: no
/// withdrawal exists without a hold behind it.
/// </para>
/// <para>
/// <b>The fee is decided when the request is made and never afterwards.</b> A member who is told they
/// will receive ninety-eight pounds must receive ninety-eight pounds even if the deployment reprices
/// withdrawals the next morning. The gross, the fee and the net are three stored figures rather than
/// two and an arithmetic assumption.
/// </para>
/// <para>
/// <b>Nothing here holds a credential.</b> The destination is a server-issued identifier and a
/// masked label; the account number lives with the settlement provider. A platform that never stores
/// one cannot leak one, which is a structural guarantee rather than a policy.
/// </para>
/// <para>
/// <b>A reversal is a new fact, never an undo.</b> Money that went out and came back is a payout and
/// a compensating credit, both on the ledger. Rewinding the original would leave the platform unable
/// to answer why the member's bank saw two movements.
/// </para>
/// </summary>
public sealed class Withdrawal : AggregateRoot<WithdrawalId>, IAuditableEntity
{
    private Withdrawal(
        WithdrawalId id,
        Guid requestedByUserId,
        FinancialAccountId accountId,
        WalletId walletId,
        PayoutDestinationId destinationId,
        string destinationLabel,
        Money gross,
        Money fee,
        DateTimeOffset requestedAt) : base(id)
    {
        RequestedByUserId = requestedByUserId;
        AccountId = accountId;
        WalletId = walletId;
        DestinationId = destinationId;
        DestinationLabel = destinationLabel;
        GrossMinorUnits = gross.MinorUnits;
        FeeMinorUnits = fee.MinorUnits;
        Currency = gross.Currency;
        Exponent = gross.Exponent;
        Status = WithdrawalStatus.Requested;
        RequestedAt = requestedAt;
        CreatedAt = requestedAt;
    }

    private Withdrawal() { }

    public Guid RequestedByUserId { get; private set; }

    public FinancialAccountId AccountId { get; private set; } = default!;

    /// <summary>The wallet the money is held out of and eventually leaves.</summary>
    public WalletId WalletId { get; private set; } = default!;

    public PayoutDestinationId DestinationId { get; private set; } = default!;

    /// <summary>What the member sees. "Barclays ••••4417". Never the number behind it.</summary>
    public string DestinationLabel { get; private set; } = string.Empty;

    /// <summary>What leaves the member's balance.</summary>
    public long GrossMinorUnits { get; private set; }

    /// <summary>What the platform keeps. Fixed at request time — see the class summary.</summary>
    public long FeeMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    public WithdrawalStatus Status { get; private set; }

    /// <summary>The settlement provider's own handle, once one has taken the instruction.</summary>
    public string? SettlementReference { get; private set; }

    public string? SettlementProvider { get; private set; }

    public string? FailureCode { get; private set; }

    public string? FailureMessage { get; private set; }

    /// <summary>Unique when present. What makes a double-tapped withdraw button one withdrawal.</summary>
    public string? IdempotencyKey { get; private set; }

    public string? CountryCode { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? ProcessingStartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? TerminatedAt { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Gross => Money.FromMinorUnits(GrossMinorUnits, Currency, Exponent);

    public Money Fee => Money.FromMinorUnits(FeeMinorUnits, Currency, Exponent);

    /// <summary>What the member actually receives. Stored as two figures, never as an assumption.</summary>
    public Money Net =>
        Money.FromMinorUnits(Math.Max(0, GrossMinorUnits - FeeMinorUnits), Currency, Exponent);

    /// <summary>Whether the amount is still held rather than gone or given back.</summary>
    public bool IsHeld => Status is WithdrawalStatus.Requested or WithdrawalStatus.Processing;

    public bool IsTerminal => Status is WithdrawalStatus.Completed
        or WithdrawalStatus.Failed
        or WithdrawalStatus.Cancelled
        or WithdrawalStatus.Reversed;

    public static Withdrawal Request(
        WithdrawalId id,
        Guid requestedByUserId,
        FinancialAccountId accountId,
        WalletId walletId,
        PayoutDestinationId destinationId,
        string destinationLabel,
        Money gross,
        Money fee,
        DateTimeOffset requestedAt,
        string? idempotencyKey = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(walletId);
        ArgumentNullException.ThrowIfNull(destinationId);
        ArgumentNullException.ThrowIfNull(gross);
        ArgumentNullException.ThrowIfNull(fee);

        if (gross.IsZero)
        {
            throw new DomainException(
                "A withdrawal of nothing is not a withdrawal.",
                FinancialErrorCodes.AmountInvalid);
        }

        if (!gross.SharesCurrencyWith(fee))
        {
            throw new DomainException(
                $"A {fee.Currency} fee cannot be charged on a {gross.Currency} withdrawal.",
                FinancialErrorCodes.CurrencyMismatch);
        }

        if (fee.MinorUnits >= gross.MinorUnits)
        {
            throw new DomainException(
                "A withdrawal whose fee is the whole amount would send the member nothing.",
                FinancialErrorCodes.AmountInvalid);
        }

        var withdrawal = new Withdrawal(
            id, requestedByUserId, accountId, walletId, destinationId,
            destinationLabel.Trim(), gross, fee, requestedAt)
        {
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant()
        };

        // BSEC-007, 10 Nov 2026. Nothing was raised here, so the one moment a member most needs to
        // hear about — somebody asked to take money out of their wallet — was the only moment in
        // the lifecycle that announced nothing. WithdrawalStatusChangedIntegrationEvent has always
        // documented itself as covering "requested, taken up, paid, refused or reversed"; this is
        // the first of those actually happening.
        //
        // Raised here rather than through Transition, which exists for moves between two real
        // statuses and would have to be told that the previous one was null.
        withdrawal.RaiseDomainEvent(new WithdrawalStatusChangedDomainEvent
        {
            WithdrawalId = withdrawal.Id,
            RequestedByUserId = withdrawal.RequestedByUserId,
            WalletId = withdrawal.WalletId,
            PreviousStatus = null,
            Status = withdrawal.Status,
            GrossMinorUnits = withdrawal.GrossMinorUnits,
            FeeMinorUnits = withdrawal.FeeMinorUnits,
            Currency = withdrawal.Currency,
            DestinationLabel = withdrawal.DestinationLabel,
            ChangedAt = requestedAt
        });

        return withdrawal;
    }

    /// <summary>
    /// A settlement provider has taken the instruction.
    /// <para>
    /// The member can no longer cancel from here. Letting them would mean releasing a hold on money
    /// that may already be in flight, which is how a platform pays somebody twice.
    /// </para>
    /// </summary>
    public void MarkProcessing(string provider, string? settlementReference, DateTimeOffset now)
    {
        Require(WithdrawalStatus.Requested);

        Status = WithdrawalStatus.Processing;
        SettlementProvider = Trimmed(provider);
        SettlementReference = Trimmed(settlementReference);
        ProcessingStartedAt = now;
        Transition(WithdrawalStatus.Requested, now);
    }

    /// <summary>The provider confirmed it. The hold becomes a payout.</summary>
    public void MarkCompleted(DateTimeOffset now, string? settlementReference = null)
    {
        Require(WithdrawalStatus.Processing, WithdrawalStatus.Requested);

        var previous = Status;

        Status = WithdrawalStatus.Completed;
        CompletedAt = now;
        TerminatedAt = now;

        if (Trimmed(settlementReference) is { } reference)
        {
            SettlementReference = reference;
        }

        Transition(previous, now);
    }

    /// <summary>The provider refused it. The hold goes back to available.</summary>
    public void MarkFailed(string? failureCode, string? failureMessage, DateTimeOffset now)
    {
        Require(WithdrawalStatus.Processing, WithdrawalStatus.Requested);

        var previous = Status;

        Status = WithdrawalStatus.Failed;
        FailureCode = Trimmed(failureCode);
        FailureMessage = Trimmed(failureMessage);
        TerminatedAt = now;

        Transition(previous, now);
    }

    /// <summary>
    /// The provider was asked and the answer is not known.
    /// <para>
    /// <b>Deliberately not a status change.</b> <c>Processing</c> already means "handed to a
    /// settlement provider and the outcome is unknown", so this records <em>why</em> it is still
    /// unknown without pretending anything moved. Releasing the hold here would be the platform
    /// giving a member their balance back while a provider may be sending them the same money.
    /// </para>
    /// <para>
    /// No domain event: the status did not change, and a <c>StatusChanged</c> event whose previous
    /// and current values match is a message consumers would act on twice.
    /// </para>
    /// </summary>
    public void RecordUnresolvedOutcome(string? code, string? message, DateTimeOffset now)
    {
        Require(WithdrawalStatus.Processing);

        FailureCode = Trimmed(code);
        FailureMessage = Trimmed(message);
        Revision++;
        ModifiedAt = now;
    }

    /// <summary>The member changed their mind before anybody picked it up.</summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status != WithdrawalStatus.Requested)
        {
            throw new DomainException(
                Status == WithdrawalStatus.Processing
                    ? "That withdrawal is already being paid out and can no longer be cancelled."
                    : $"A {Status} withdrawal cannot be cancelled.",
                FinancialErrorCodes.WithdrawalNotCancellable);
        }

        Status = WithdrawalStatus.Cancelled;
        TerminatedAt = now;

        Transition(WithdrawalStatus.Requested, now);
    }

    /// <summary>It went out and came back. A new fact, never an undo — see the class summary.</summary>
    public void MarkReversed(string? failureCode, string? failureMessage, DateTimeOffset now)
    {
        Require(WithdrawalStatus.Completed);

        Status = WithdrawalStatus.Reversed;
        FailureCode = Trimmed(failureCode);
        FailureMessage = Trimmed(failureMessage);
        TerminatedAt = now;

        Transition(WithdrawalStatus.Completed, now);
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Require(params WithdrawalStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
        {
            throw new DomainException(
                $"A withdrawal in '{Status}' cannot make that transition. "
                + $"Expected one of: {string.Join(", ", allowed)}.",
                FinancialErrorCodes.WithdrawalTransitionInvalid);
        }
    }

    private void Transition(WithdrawalStatus previous, DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;

        RaiseDomainEvent(new WithdrawalStatusChangedDomainEvent
        {
            WithdrawalId = Id,
            RequestedByUserId = RequestedByUserId,
            WalletId = WalletId,
            PreviousStatus = previous,
            Status = Status,
            GrossMinorUnits = GrossMinorUnits,
            FeeMinorUnits = FeeMinorUnits,
            Currency = Currency,
            DestinationLabel = DestinationLabel,
            FailureCode = FailureCode,
            ChangedAt = now
        });
    }
}
