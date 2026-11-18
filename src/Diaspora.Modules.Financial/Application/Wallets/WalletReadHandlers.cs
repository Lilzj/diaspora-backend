using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Transactions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>
/// One transaction of the caller's, with the entries that made it up.
/// <para>
/// <b>Ownership is decided by the entries, not by the transaction.</b> A transaction has an
/// initiator, but a member can legitimately be a party to one they did not initiate — a refund a
/// staff member issued, a placement ended by a sweep. The question the platform can actually answer
/// is "did this move money in a wallet you hold", and that is what is asked.
/// </para>
/// <para>
/// <b>A member sees only their own legs.</b> A capture writes two entries, one against the
/// platform's clearing wallet; serving both to a member would put the platform's clearing position
/// on a phone. Staff with <c>financial:wallet:read:any</c> see everything.
/// </para>
/// </summary>
public sealed class GetTransactionQueryHandler
    : IQueryHandler<GetTransactionQuery, TransactionDetailResponse>
{
    private readonly IFinancialTransactionRepository _transactions;
    private readonly ILedgerEntryRepository _entries;
    private readonly IWalletRepository _wallets;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetTransactionQueryHandler(
        IFinancialTransactionRepository transactions,
        ILedgerEntryRepository entries,
        IWalletRepository wallets,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _transactions = transactions;
        _entries = entries;
        _wallets = wallets;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<TransactionDetailResponse>> Handle(
        GetTransactionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<TransactionDetailResponse>(caller.Error);
        }

        var transaction = await _transactions.FindAsync(
            TransactionId.From(query.TransactionId), cancellationToken);

        if (transaction is null)
        {
            return Result.Failure<TransactionDetailResponse>(Error.NotFound(
                FinancialErrorCodes.TransactionNotFound, "That transaction does not exist."));
        }

        var entries = await _entries.ListByTransactionAsync(transaction.Id, cancellationToken);
        var isStaff = _authorization.Has(FinancialPermissions.WalletReadAny);

        var visible = isStaff
            ? entries
            : await MineAsync(caller.Value, entries, cancellationToken);

        // 404 rather than 403 on a transaction with no leg of the caller's, so an identifier cannot
        // be used to discover that a transaction exists.
        if (visible.Count == 0 && transaction.InitiatedByUserId != caller.Value && !isStaff)
        {
            return Result.Failure<TransactionDetailResponse>(Error.NotFound(
                FinancialErrorCodes.TransactionNotFound, "That transaction does not exist."));
        }

        var symbol = await _currencies.SymbolAsync(transaction.Currency, cancellationToken);

        return Result.Success(new TransactionDetailResponse
        {
            Transaction = FinancialMapper.ToResponse(transaction, symbol),
            Entries = [.. visible.Select(entry => FinancialMapper.ToResponse(entry, symbol))]
        });
    }

    private async Task<IReadOnlyList<LedgerEntry>> MineAsync(
        Guid userId, IReadOnlyList<LedgerEntry> entries, CancellationToken cancellationToken)
    {
        var mine = await _wallets.ListByOwnerAsync(userId, cancellationToken);
        var walletIds = mine.Select(wallet => wallet.Id).ToHashSet();

        return [.. entries.Where(entry => walletIds.Contains(entry.WalletId))];
    }
}

/// <summary>
/// A receipt, by its reference — and only to somebody entitled to it.
/// <para>
/// <b>Two different answers to "who may read this", because there are two different receipts.</b> A
/// gift with a giver is served to that giver and to nobody else. A gift with no giver cannot be
/// scoped to anybody, so it is served to whoever presents the reference — which is sound only
/// because such a reference is minted at 130 bits and is the single handle its giver was ever
/// issued.
/// </para>
/// <para>
/// <b>Until BBL-004 the first case did not exist.</b> Any member holding
/// <c>FinancialPermissions.GivingRead</c> could read any receipt, and the comment that stood here
/// defended that by asserting "the generator makes them unguessable" — which was untrue. The
/// reference was six characters from a 32-symbol alphabet: a uniform 30 bits, so roughly a million
/// requests found a given day's receipts and an afternoon harvested the population. The response
/// carries no donor and no message, so what that bought was not personal data but the platform's
/// funding intelligence: which causes attract money, in what amounts, from where.
/// </para>
/// <para>
/// <b>A refusal and a miss are the same answer</b>, from <see cref="NoSuchReceipt"/>, so a caller
/// cannot use the difference to confirm that a reference exists. That is the only reason refusing
/// with <c>404</c> rather than <c>403</c> is right here.
/// </para>
/// </summary>
public sealed class GetReceiptQueryHandler : IQueryHandler<GetReceiptQuery, ReceiptResponse>
{
    private readonly IDonationRepository _donations;
    private readonly IDonationCauseRepository _causes;
    private readonly ICommunityContributionRepository _contributions;
    private readonly IContributionFundRepository _funds;
    private readonly CurrencyCatalog _currencies;
    private readonly FinancialAuthorizationService _authorization;
    private readonly ILogger<GetReceiptQueryHandler> _logger;

    public GetReceiptQueryHandler(
        IDonationRepository donations,
        IDonationCauseRepository causes,
        ICommunityContributionRepository contributions,
        IContributionFundRepository funds,
        CurrencyCatalog currencies,
        FinancialAuthorizationService authorization,
        ILogger<GetReceiptQueryHandler> logger)
    {
        _donations = donations;
        _causes = causes;
        _contributions = contributions;
        _funds = funds;
        _currencies = currencies;
        _authorization = authorization;
        _logger = logger;
    }

    public async Task<Result<ReceiptResponse>> Handle(
        GetReceiptQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.ReceiptReference))
        {
            return NoSuchReceipt();
        }

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<ReceiptResponse>(caller.Error);
        }

        var reference = query.ReceiptReference.Trim();

        if (await _donations.FindByReceiptAsync(reference, cancellationToken) is { } donation)
        {
            if (!MayRead(donation.DonorUserId, caller.Value))
            {
                return Refuse("donation", caller.Value);
            }

            var cause = donation.CauseId is { } causeId
                ? await _causes.FindAsync(causeId, cancellationToken)
                : null;

            var symbol = await _currencies.SymbolAsync(donation.Currency, cancellationToken);

            return Result.Success(new ReceiptResponse
            {
                ReceiptReference = reference,
                Kind = "Donation",
                Status = donation.Status.ToString(),
                Amount = FinancialMapper.ToResponse(donation.Amount, symbol),
                DestinationCode = cause?.Code,
                DestinationName = cause?.Title,

                // Absent, not hidden. The aggregate stored nothing when anonymous.
                IsAnonymous = donation.IsAnonymous,
                CountryCode = donation.CountryCode,
                IssuedAt = donation.CompletedAt ?? donation.CreatedAt
            });
        }

        if (await _contributions.FindByReceiptAsync(reference, cancellationToken)
            is { } contribution)
        {
            if (!MayRead(contribution.ContributorUserId, caller.Value))
            {
                return Refuse("contribution", caller.Value);
            }

            var fund = await _funds.FindAsync(contribution.FundId, cancellationToken);
            var symbol = contribution.Amount is { } money
                ? await _currencies.SymbolAsync(money.Currency, cancellationToken)
                : null;

            return Result.Success(new ReceiptResponse
            {
                ReceiptReference = reference,
                Kind = "Contribution",
                Status = contribution.Status.ToString(),

                // A contribution of goods, time or skills has no amount at all. A zero would read as
                // a gift of nothing; the currency the platform defaults to is not a claim it should
                // make about somebody's coat drive.
                Amount = FinancialMapper.ToResponse(
                    contribution.Amount ?? Money.Zero("XXX"), symbol),
                DestinationCode = fund?.Code,
                DestinationName = fund?.Name,
                IsAnonymous = contribution.IsAnonymous,
                CountryCode = contribution.CountryCode,
                IssuedAt = contribution.CompletedAt ?? contribution.CreatedAt
            });
        }

        _logger.LogWarning(
            "Receipt lookup by {Caller} matched no receipt. Repeated misses from one caller are "
            + "what enumeration looks like.",
            caller.Value);

        return NoSuchReceipt();
    }

    /// <summary>
    /// Whether <paramref name="caller"/> may read a receipt owned by <paramref name="owner"/>.
    /// <para>
    /// <b>Keyed on the identifier rather than on <c>IsAnonymous</c>, which is the stronger of the
    /// two tests</b> — the same reasoning B13's receipt notification uses. Both aggregates store
    /// <c>isAnonymous ? null : id</c>, so an anonymous gift always lacks the identifier; keying on
    /// the flag would serve a receipt to the world if a future path ever set one without the other.
    /// </para>
    /// </summary>
    private static bool MayRead(Guid? owner, Guid caller) => owner is null || owner == caller;

    /// <summary>
    /// Logs the refusal and returns the same answer a miss returns.
    /// <para>
    /// <b>The reference is deliberately not in the log line.</b> For an ownerless receipt the
    /// reference is the credential, so writing it to a log would move the secret somewhere with a
    /// different and longer-lived audience — which is the whole mistake this finding is about,
    /// repeated into the observability stack. The caller and the kind are enough to see a probe.
    /// </para>
    /// </summary>
    private Result<ReceiptResponse> Refuse(string kind, Guid caller)
    {
        _logger.LogWarning(
            "Receipt lookup refused: {Caller} presented a reference for a {Kind} receipt belonging "
            + "to somebody else.",
            caller,
            kind);

        return NoSuchReceipt();
    }

    /// <summary>
    /// The one answer for every failure: no such reference, a wrong owner, and a blank string.
    /// <para>
    /// One method rather than three call sites so the three cannot drift apart. A refusal that
    /// differed from a miss in its code, its message or its status would tell a caller which
    /// references exist, and the enumeration this finding is about would survive the fix that was
    /// supposed to end it.
    /// </para>
    /// </summary>
    private static Result<ReceiptResponse> NoSuchReceipt() =>
        Result.Failure<ReceiptResponse>(Error.NotFound(
            FinancialErrorCodes.DonationNotFound, "No receipt has that reference."));
}

/// <summary>
/// Sums a member's ledger over a period, on the server.
/// <para>
/// <b>Grouped by currency, direction and entry type, and never totalled across currencies.</b> The
/// platform does not convert, so a single total would need a rate it has no authority to pick.
/// </para>
/// <para>
/// <b>Bounded, and says so when it is.</b> A member with a hundred thousand entries would otherwise
/// turn one screen into a full-table scan; the sweep reads a bounded window per wallet and reports
/// <c>isTruncated</c> rather than presenting a partial total as a complete one — which is exactly the
/// failure the client-side version had and could not detect.
/// </para>
/// </summary>
public sealed class GetWalletAnalyticsQueryHandler
    : IQueryHandler<GetWalletAnalyticsQuery, WalletAnalyticsResponse>
{
    /// <summary>
    /// The most entries one wallet contributes to a rollup. Well above a heavy year and well below
    /// what would make this route a way to load the whole ledger.
    /// </summary>
    private const int MaxEntriesPerWallet = 5_000;

    private readonly IWalletRepository _wallets;
    private readonly ILedgerEntryRepository _entries;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;

    public GetWalletAnalyticsQueryHandler(
        IWalletRepository wallets,
        ILedgerEntryRepository entries,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IClock clock)
    {
        _wallets = wallets;
        _entries = entries;
        _authorization = authorization;
        _currencies = currencies;
        _clock = clock;
    }

    public async Task<Result<WalletAnalyticsResponse>> Handle(
        GetWalletAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<WalletAnalyticsResponse>(caller.Error);
        }

        var now = _clock.UtcNow;
        var to = query.To ?? now;
        var from = query.From ?? to.AddDays(-30);

        if (from >= to)
        {
            return Result.Failure<WalletAnalyticsResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid,
                "The start of the period has to come before its end."));
        }

        var wallets = await _wallets.ListByOwnerAsync(caller.Value, cancellationToken);
        var symbols = await _currencies.AllAsync(cancellationToken);
        var buckets = new Dictionary<(string Currency, string Direction, string EntryType), (long Total, int Count)>();
        var truncated = false;

        foreach (var wallet in wallets)
        {
            var entries = await _entries.ListForPeriodAsync(wallet.Id, from, to, cancellationToken);

            if (entries.Count >= MaxEntriesPerWallet)
            {
                truncated = true;
                entries = [.. entries.Take(MaxEntriesPerWallet)];
            }

            foreach (var entry in entries)
            {
                var key = (
                    entry.Currency, entry.Direction.ToString(), entry.EntryType.ToString());

                var current = buckets.TryGetValue(key, out var existing) ? existing : (0L, 0);

                buckets[key] = (current.Item1 + entry.AmountMinorUnits, current.Item2 + 1);
            }
        }

        var totals = buckets
            .OrderBy(bucket => bucket.Key.Currency, StringComparer.Ordinal)
            .ThenBy(bucket => bucket.Key.Direction, StringComparer.Ordinal)
            .ThenBy(bucket => bucket.Key.EntryType, StringComparer.Ordinal)
            .Select(bucket => new WalletAnalyticsTotalResponse
            {
                Currency = bucket.Key.Currency,
                Direction = bucket.Key.Direction,
                EntryType = bucket.Key.EntryType,
                Amount = FinancialMapper.ToResponse(
                    Money.FromMinorUnits(
                        bucket.Value.Total,
                        bucket.Key.Currency,
                        symbols.TryGetValue(bucket.Key.Currency, out var facts)
                            ? facts.Exponent
                            : Money.DefaultExponent),
                    symbols.TryGetValue(bucket.Key.Currency, out var symbolFacts)
                        ? symbolFacts.Symbol
                        : null),
                Count = bucket.Value.Count
            })
            .ToList();

        return Result.Success(new WalletAnalyticsResponse
        {
            PeriodStart = from,
            PeriodEnd = to,
            Totals = totals,
            Wallets =
            [
                .. wallets.Select(wallet => FinancialMapper.ToResponse(
                    wallet,
                    symbols.TryGetValue(wallet.Currency, out var facts) ? facts.Symbol : null))
            ],
            IsTruncated = truncated,
            ComputedAt = now
        });
    }
}
