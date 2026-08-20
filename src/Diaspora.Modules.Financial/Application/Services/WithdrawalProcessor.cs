using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// Moves money for a payout, and is the only thing that does.
/// <para>
/// <b>Three postings, in a fixed order, and every one of them balances.</b> A hold moves the amount
/// from the member's available balance into their reserved balance — two legs on one wallet, which
/// the posting service handles by folding the first forward before reading for the second. A release
/// is the same pair backwards. A payout debits the reserved balance and credits the platform's
/// clearing wallet, which is the counterparty for everything crossing the platform's boundary.
/// </para>
/// <para>
/// <b>The hold is what makes concurrent withdrawals safe, and it is safe for two reasons that both
/// have to hold.</b> The posting service refuses a debit the available balance cannot cover, so the
/// second of two simultaneous requests for the same money fails its check; and the wallet's revision
/// is a concurrency token, so if both passed the check against the same stale read only one commits.
/// Either alone would leave a window. Neither is implemented here — this type's job is to make sure
/// every path goes through them.
/// </para>
/// <para>
/// <b>The fee is a separate leg, not a smaller payout.</b> The member's reserved balance gives up the
/// gross; the platform's revenue wallet takes the fee and the clearing wallet takes the net. Netting
/// them would lose the fee as a fact, and "what did the platform earn on payouts" would have to be
/// inferred from the difference between two numbers that were never recorded apart.
/// </para>
/// </summary>
public sealed class WithdrawalProcessor
{
    private readonly ILedgerPostingService _ledger;
    private readonly FinancialIdentityService _identities;

    public WithdrawalProcessor(ILedgerPostingService ledger, FinancialIdentityService identities)
    {
        _ledger = ledger;
        _identities = identities;
    }

    /// <summary>
    /// Takes the amount out of available and puts it into reserved.
    /// <para>
    /// Fails when the balance cannot cover it, which is the refusal a member sees as "insufficient
    /// funds" — and is the same refusal whether they asked once for too much or twice for enough.
    /// </para>
    /// </summary>
    public async Task<Result> HoldAsync(
        Withdrawal withdrawal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(withdrawal);

        return await PostAsync(
            withdrawal,
            TransactionType.Withdrawal,
            $"Withdrawal held: {withdrawal.DestinationLabel}",
            $"withdrawal-hold:{withdrawal.Id.Value:N}",
            [
                Leg(withdrawal, LedgerDirection.Debit, LedgerBucket.Available, LedgerEntryType.Hold),
                Leg(withdrawal, LedgerDirection.Credit, LedgerBucket.Reserved, LedgerEntryType.Hold)
            ],
            cancellationToken);
    }

    /// <summary>Puts a held amount back where it came from. A refusal or a cancellation.</summary>
    public async Task<Result> ReleaseAsync(
        Withdrawal withdrawal, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(withdrawal);

        return await PostAsync(
            withdrawal,
            TransactionType.Reversal,
            $"Withdrawal released: {reason}",
            $"withdrawal-release:{withdrawal.Id.Value:N}",
            [
                Leg(withdrawal, LedgerDirection.Debit, LedgerBucket.Reserved, LedgerEntryType.Release),
                Leg(withdrawal, LedgerDirection.Credit, LedgerBucket.Available, LedgerEntryType.Release)
            ],
            cancellationToken);
    }

    /// <summary>
    /// The money leaves. Reserved is debited; the fee goes to revenue and the net to clearing.
    /// </summary>
    public async Task<Result> PayOutAsync(
        Withdrawal withdrawal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(withdrawal);

        var clearing = await _identities.PlatformWalletAsync(
            WalletType.Clearing, withdrawal.Currency, cancellationToken);

        if (clearing is null)
        {
            return Result.Failure(Error.Unexpected(
                FinancialErrorCodes.WalletNotFound,
                "The platform has no clearing wallet in this currency."));
        }

        var legs = new List<LedgerPostingLeg>
        {
            Leg(withdrawal, LedgerDirection.Debit, LedgerBucket.Reserved, LedgerEntryType.Payout)
        };

        if (withdrawal.Fee.IsZero)
        {
            legs.Add(new LedgerPostingLeg
            {
                WalletId = clearing.Id,
                Direction = LedgerDirection.Credit,
                Amount = withdrawal.Gross,
                EntryType = LedgerEntryType.Payout
            });
        }
        else
        {
            var revenue = await _identities.PlatformWalletAsync(
                WalletType.Revenue, withdrawal.Currency, cancellationToken);

            if (revenue is null)
            {
                return Result.Failure(Error.Unexpected(
                    FinancialErrorCodes.WalletNotFound,
                    "The platform has no revenue wallet in this currency to take the fee into."));
            }

            legs.Add(new LedgerPostingLeg
            {
                WalletId = clearing.Id,
                Direction = LedgerDirection.Credit,
                Amount = withdrawal.Net,
                EntryType = LedgerEntryType.Payout
            });

            legs.Add(new LedgerPostingLeg
            {
                WalletId = revenue.Id,
                Direction = LedgerDirection.Credit,
                Amount = withdrawal.Fee,
                EntryType = LedgerEntryType.Fee
            });
        }

        return await PostAsync(
            withdrawal,
            TransactionType.Payout,
            $"Withdrawal paid: {withdrawal.DestinationLabel}",
            $"withdrawal-payout:{withdrawal.Id.Value:N}",
            legs,
            cancellationToken);
    }

    /// <summary>
    /// A completed payout came back.
    /// <para>
    /// A compensating credit rather than an undo: the money left and returned, and the member's
    /// statement has to show both or a support agent cannot explain the two movements their bank
    /// saw. Credited to available, because a returned payout is spendable again — there is nothing
    /// left holding it.
    /// </para>
    /// </summary>
    public async Task<Result> ReverseAsync(
        Withdrawal withdrawal, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(withdrawal);

        var clearing = await _identities.PlatformWalletAsync(
            WalletType.Clearing, withdrawal.Currency, cancellationToken);

        if (clearing is null)
        {
            return Result.Failure(Error.Unexpected(
                FinancialErrorCodes.WalletNotFound,
                "The platform has no clearing wallet in this currency."));
        }

        // Only the net comes back. The fee was earned on a payout the platform did perform, and
        // whether to return it is a support decision rather than an accounting consequence.
        return await PostAsync(
            withdrawal,
            TransactionType.Reversal,
            $"Withdrawal reversed: {reason}",
            $"withdrawal-reversal:{withdrawal.Id.Value:N}",
            [
                new LedgerPostingLeg
                {
                    WalletId = clearing.Id,
                    Direction = LedgerDirection.Debit,
                    Amount = withdrawal.Net,
                    EntryType = LedgerEntryType.Reversal
                },
                new LedgerPostingLeg
                {
                    WalletId = withdrawal.WalletId,
                    Direction = LedgerDirection.Credit,
                    Bucket = LedgerBucket.Available,
                    Amount = withdrawal.Net,
                    EntryType = LedgerEntryType.Reversal
                }
            ],
            cancellationToken);
    }

    private static LedgerPostingLeg Leg(
        Withdrawal withdrawal,
        LedgerDirection direction,
        LedgerBucket bucket,
        LedgerEntryType entryType) =>
        new()
        {
            WalletId = withdrawal.WalletId,
            Direction = direction,
            Bucket = bucket,
            Amount = withdrawal.Gross,
            EntryType = entryType
        };

    private async Task<Result> PostAsync(
        Withdrawal withdrawal,
        TransactionType transactionType,
        string description,
        string reference,
        IReadOnlyList<LedgerPostingLeg> legs,
        CancellationToken cancellationToken)
    {
        var posting = await _ledger.PostAsync(
            new LedgerPostingRequest
            {
                TransactionType = transactionType,
                Amount = withdrawal.Gross,
                Description = description,
                Audit = AuditStamp.Create(
                    FinancialActor.Member(withdrawal.RequestedByUserId),
                    FinancialSource.Api,
                    description),
                Legs = legs,
                InitiatedByUserId = withdrawal.RequestedByUserId,

                // The reference is derived from the withdrawal rather than generated, so a retried
                // pass of the payout sweep replays the original posting instead of moving the money
                // again. The ledger answers with the transaction it already wrote.
                Reference = reference,
                CountryCode = withdrawal.CountryCode
            },
            cancellationToken);

        return posting.IsSuccess ? Result.Success() : Result.Failure(posting.Error);
    }
}
