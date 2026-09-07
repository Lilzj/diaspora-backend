using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Transactions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Disbursements;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Infrastructure.Disbursements;

/// <summary>
/// Moves platform revenue into a member's own wallet.
/// <para>
/// <b>The half of a creator's earning that was missing.</b> A donation naming a member as its
/// beneficiary posts the clearing wallet to platform revenue, and an earning is recorded against the
/// member — a claim, correctly, since it has not cleared. Nothing then moved the money: the earning
/// sat at <c>Pending</c> for ever, the dashboard promised a clearing date nothing kept, and the
/// balance a creator could see was one they could never reach.
/// </para>
/// <para>
/// <b>Two legs, and the revenue wallet is the one that pays.</b> The platform holds the money until
/// the earning clears, so revenue is debited and the member credited. It is a
/// <see cref="TransactionType.Payout"/> because that is what it is from the member's side; the
/// withdrawal they raise afterwards is a separate movement out of Diaspora.
/// </para>
/// </summary>
internal sealed class PlatformDisbursement : IPlatformDisbursement
{
    private const string Component = "platform-disbursement";

    private readonly FinancialIdentityService _identities;
    private readonly ILedgerPostingService _ledger;
    private readonly IFinancialTransactionRepository _transactions;

    public PlatformDisbursement(
        FinancialIdentityService identities,
        ILedgerPostingService ledger,
        IFinancialTransactionRepository transactions)
    {
        _identities = identities;
        _ledger = ledger;
        _transactions = transactions;
    }

    public async Task<Result<PlatformDisbursementReceipt>> PayAsync(
        PlatformDisbursementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AmountMinorUnits <= 0)
        {
            return Result.Failure<PlatformDisbursementReceipt>(Error.Validation(
                FinancialErrorCodes.AmountInvalid, "There is nothing to pay."));
        }

        var amount = Money.FromMinorUnits(
            request.AmountMinorUnits, request.Currency, request.Exponent);

        // **Asked before posting, not inferred from the answer.**
        //
        // `PostAsync` is idempotent on the reference and replies with the original transaction, but
        // it replies with that transaction's entries too — so a replay and a fresh posting are
        // indistinguishable from the result. A sweep that could not tell them apart would report a
        // batch of the wrong size every time it retried.
        var already = await _transactions.FindByReferenceAsync(request.Reference, cancellationToken);

        if (already is not null)
        {
            return Result.Success(new PlatformDisbursementReceipt
            {
                TransactionId = already.Id.Value,
                Posted = false
            });
        }

        // Revenue is where a capture left the money, so revenue is what pays it out. A missing
        // wallet here is a platform fault, not the member's, and is named as such.
        var revenue = await _identities.PlatformWalletAsync(
            WalletType.Revenue, amount.Currency, cancellationToken);

        if (revenue is null)
        {
            return Result.Failure<PlatformDisbursementReceipt>(Error.Unexpected(
                FinancialErrorCodes.WalletNotFound,
                $"The platform holds no {amount.Currency} revenue wallet to pay from."));
        }

        // Opened if they have none. A member who earned before they ever held a wallet is ordinary —
        // a donation names a beneficiary, and nothing requires them to have transacted first.
        var account = await _identities.GetOrCreateAsync(
            request.BeneficiaryUserId,
            FinancialAccountType.Personal,
            amount.Currency,
            cancellationToken: cancellationToken);

        var wallet = await _identities.PrimaryWalletAsync(
            account, amount.Currency, cancellationToken);

        if (wallet is null)
        {
            return Result.Failure<PlatformDisbursementReceipt>(Error.Unexpected(
                FinancialErrorCodes.WalletNotFound,
                $"No {amount.Currency} wallet could be opened for the member."));
        }

        var posting = await _ledger.PostAsync(
            new LedgerPostingRequest
            {
                TransactionType = TransactionType.Payout,
                Amount = amount,
                Description = request.Description,
                Audit = AuditStamp.Create(
                    // System, not Member: the member did not ask for this and cannot decline it.
                    // Attributing it to them would put an action in their audit trail they never took.
                    FinancialActor.System(Component),
                    FinancialSource.System,
                    request.Reason ?? request.Description),
                Legs =
                [
                    new LedgerPostingLeg
                    {
                        WalletId = revenue.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = amount,
                        EntryType = LedgerEntryType.Payout
                    },
                    new LedgerPostingLeg
                    {
                        WalletId = wallet.Id,
                        Direction = LedgerDirection.Credit,
                        Amount = amount,
                        EntryType = LedgerEntryType.Payout
                    }
                ],

                // The member is the party the transaction is for, which is what makes it visible to
                // them: the transaction read is scoped to legs in the caller's own wallets.
                InitiatedByUserId = request.BeneficiaryUserId,

                // The caller's key, and the reason a crashed sweep does not pay twice.
                Reference = request.Reference
            },
            cancellationToken);

        if (posting.IsFailure)
        {
            return Result.Failure<PlatformDisbursementReceipt>(posting.Error);
        }

        return Result.Success(new PlatformDisbursementReceipt
        {
            TransactionId = posting.Value.Transaction.Id.Value,
            Posted = true
        });
    }
}
