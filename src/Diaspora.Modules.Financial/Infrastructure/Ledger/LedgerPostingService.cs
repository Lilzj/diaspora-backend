using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Transactions;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Infrastructure.Ledger;

/// <summary>
/// The one implementation of the one door into the ledger.
/// <para>
/// <b>Everything the platform guarantees about money is enforced here, once.</b> The legs must
/// balance per currency; every wallet must exist, be in the right currency and be willing to take
/// the movement; the sequence is taken from the wallet so a statement has no gaps; the balance
/// before is read from the projection so an entry computed against a stale read is refused rather
/// than silently corrupting it. An architecture test asserts there is exactly one implementation,
/// because the enforcement is only as good as the number of ways around it.
/// </para>
/// <para>
/// <b>It stages and does not save.</b> A posting is almost never the only thing a command does — a
/// purchase writes a purchase row, a placement and a posting, and all three land together or none
/// of them. Commands that post declare <c>[Transactional]</c> on the request.
/// </para>
/// <para>
/// <b>Wallets are loaded once and reused across legs.</b> Two legs against one wallet in a single
/// posting is ordinary — a hold is a debit of available and a credit of reserved — and loading the
/// wallet twice would produce two tracked instances, of which EF would write one.
/// </para>
/// </summary>
public sealed class LedgerPostingService : ILedgerPostingService
{
    private readonly IWalletRepository _wallets;
    private readonly ILedgerEntryRepository _entries;
    private readonly IFinancialTransactionRepository _transactions;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public LedgerPostingService(
        IWalletRepository wallets,
        ILedgerEntryRepository entries,
        IFinancialTransactionRepository transactions,
        IGuidGenerator guids,
        IClock clock)
    {
        _wallets = wallets;
        _entries = entries;
        _transactions = transactions;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<LedgerPostingResult>> PostAsync(
        LedgerPostingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Legs.Count == 0)
        {
            return Result.Failure<LedgerPostingResult>(Error.Validation(
                FinancialErrorCodes.LedgerEmptyPosting,
                "A posting with no legs moves nothing and would leave a transaction with no entries."));
        }

        if (Unbalanced(request.Legs) is { } imbalance)
        {
            return Result.Failure<LedgerPostingResult>(Error.Validation(
                FinancialErrorCodes.LedgerUnbalanced, imbalance));
        }

        if (request.Reference is { } reference
            && await _transactions.FindByReferenceAsync(reference, cancellationToken) is { } existing)
        {
            // A replayed request. Answering with the original transaction is what makes the
            // idempotency key useful rather than merely safe: the caller gets the same answer twice.
            return Result.Success(new LedgerPostingResult
            {
                Transaction = existing,
                Entries = await _entries.ListByTransactionAsync(existing.Id, cancellationToken)
            });
        }

        var now = _clock.UtcNow;
        var transaction = FinancialTransaction.Begin(
            TransactionId.From(_guids.NewSequentialGuid()),
            request.TransactionType,
            request.Amount,
            request.Description,

            // Its own copy. One posting writes a transaction and several entries, and every one of
            // them owns an audit stamp with an actor inside it; sharing one instance makes EF track
            // it against whichever owner it saw first and write nulls into the others' NOT NULL
            // columns. That is the platform's owned-type sharing trap, and this is where it bites.
            request.Audit.Copy(),
            now,
            request.Subject,
            request.InitiatedByUserId,
            request.Reference,
            request.CountryCode);

        var loaded = new Dictionary<Guid, Wallet>();
        var appended = new List<LedgerEntry>(request.Legs.Count);

        foreach (var leg in request.Legs)
        {
            var walletResult = await ResolveAsync(leg, loaded, cancellationToken);

            if (walletResult.IsFailure)
            {
                return Result.Failure<LedgerPostingResult>(walletResult.Error);
            }

            var wallet = walletResult.Value;
            var check = Check(wallet, leg);

            if (check is not null)
            {
                return Result.Failure<LedgerPostingResult>(check);
            }

            var entry = LedgerEntry.Append(
                LedgerEntryId.From(_guids.NewSequentialGuid()),
                wallet.Id,
                transaction.Id,
                wallet.NextSequence,
                leg.EntryType,
                leg.Direction,
                leg.Bucket,
                leg.Amount,
                wallet.BalanceOf(leg.Bucket),
                request.Audit.Copy(),
                now,
                leg.Subject ?? request.Subject,
                leg.ExternalReference ?? request.Reference,
                wallet.AllowsNegativeBalance);

            // The projection is folded forward before the next leg reads it, so two legs against one
            // wallet in one posting compute against each other rather than against the same start.
            wallet.Apply(entry);
            _wallets.Update(wallet);

            appended.Add(entry);
        }

        transaction.Complete(appended.Count, now);

        _transactions.Add(transaction);
        _entries.AddRange(appended);

        return Result.Success(new LedgerPostingResult
        {
            Transaction = transaction,
            Entries = appended
        });
    }

    /// <summary>
    /// Whether credits and debits agree, per currency.
    /// <para>
    /// The invariant the whole platform's financial correctness rests on. Checked before anything is
    /// loaded, because an unbalanced request is a caller defect and finding out after three database
    /// round trips helps nobody.
    /// </para>
    /// </summary>
    private static string? Unbalanced(IReadOnlyList<LedgerPostingLeg> legs)
    {
        foreach (var group in legs.GroupBy(leg => leg.Amount.Currency, StringComparer.Ordinal))
        {
            var credits = group
                .Where(leg => leg.Direction == LedgerDirection.Credit)
                .Sum(leg => leg.Amount.MinorUnits);

            var debits = group
                .Where(leg => leg.Direction == LedgerDirection.Debit)
                .Sum(leg => leg.Amount.MinorUnits);

            if (credits != debits)
            {
                return $"The {group.Key} legs do not balance: {credits} credited against "
                    + $"{debits} debited. Every transaction on this platform balances, including "
                    + "money arriving from outside it — that is what the platform's clearing wallet "
                    + "is for.";
            }
        }

        return null;
    }

    private async Task<Result<Wallet>> ResolveAsync(
        LedgerPostingLeg leg, Dictionary<Guid, Wallet> loaded, CancellationToken cancellationToken)
    {
        if (loaded.TryGetValue(leg.WalletId.Value, out var cached))
        {
            return Result.Success(cached);
        }

        var wallet = await _wallets.FindAsync(leg.WalletId, cancellationToken);

        if (wallet is null)
        {
            return Result.Failure<Wallet>(Error.NotFound(
                FinancialErrorCodes.WalletNotFound,
                "One of the wallets in this posting does not exist."));
        }

        loaded[leg.WalletId.Value] = wallet;

        return Result.Success(wallet);
    }

    private static Error? Check(Wallet wallet, LedgerPostingLeg leg)
    {
        if (!string.Equals(leg.Amount.Currency, wallet.Currency, StringComparison.Ordinal))
        {
            return Error.Validation(
                FinancialErrorCodes.CurrencyMismatch,
                $"A {leg.Amount.Currency} entry cannot be applied to a {wallet.Currency} wallet. "
                + "The platform never converts.");
        }

        if (leg.Direction == LedgerDirection.Credit)
        {
            return wallet.CanCredit(leg.Amount)
                ? null
                : Error.Conflict(
                    FinancialErrorCodes.WalletClosed,
                    "That wallet is closed and cannot receive money.");
        }

        if (wallet.Status == WalletStatus.Frozen)
        {
            return Error.Conflict(
                FinancialErrorCodes.WalletFrozen,
                "That wallet is frozen. Money can arrive and nothing can leave.");
        }

        if (wallet.Status != WalletStatus.Active)
        {
            return Error.Conflict(
                FinancialErrorCodes.WalletClosed,
                "That wallet is not active and cannot be debited.");
        }

        return wallet.CanDebit(leg.Amount, leg.Bucket)
            ? null
            : Error.Conflict(
                FinancialErrorCodes.InsufficientFunds,
                $"That wallet does not hold enough in its {leg.Bucket} balance.");
    }
}
