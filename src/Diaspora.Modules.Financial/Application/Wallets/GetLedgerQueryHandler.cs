using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>
/// Reads a statement.
/// <para>
/// <b>The ownership check is here rather than at the endpoint, and it answers 404 rather than
/// 403.</b> Answering "forbidden" for a wallet that belongs to somebody else confirms the wallet
/// exists, which is an enumeration oracle on the one identifier the platform most wants to keep
/// uninteresting.
/// </para>
/// </summary>
public sealed class GetLedgerQueryHandler : IQueryHandler<GetLedgerQuery, LedgerPageResponse>
{
    private readonly ILedgerEntryRepository _entries;
    private readonly IWalletRepository _wallets;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetLedgerQueryHandler(
        ILedgerEntryRepository entries,
        IWalletRepository wallets,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _entries = entries;
        _wallets = wallets;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<LedgerPageResponse>> Handle(
        GetLedgerQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<LedgerPageResponse>(caller.Error);
        }

        var wallet = await _wallets.FindAsync(WalletId.From(query.WalletId), cancellationToken);

        if (wallet is null
            || (wallet.OwnerId != caller.Value
                && !_authorization.Has(FinancialPermissions.WalletReadAny)))
        {
            return Result.Failure<LedgerPageResponse>(Error.NotFound(
                FinancialErrorCodes.WalletNotFound, "That wallet does not exist."));
        }

        var pageSize = Math.Clamp(query.PageSize, 1, FinancialLimits.MaxLedgerPageSize);

        // One more than asked for, so "is there another page" is answered without a second count.
        var entries = await _entries.PageAsync(
            new LedgerQuery
            {
                WalletId = wallet.Id,
                AfterSequence = query.AfterSequence,
                From = query.From,
                To = query.To,
                EntryType = Enum.TryParse<LedgerEntryType>(query.EntryType, true, out var kind)
                    ? kind
                    : null,
                PageSize = Math.Min(pageSize + 1, FinancialLimits.MaxLedgerPageSize)
            },
            cancellationToken);

        var hasMore = entries.Count > pageSize;
        var page = hasMore ? entries.Take(pageSize).ToList() : entries;
        var symbol = await _currencies.SymbolAsync(wallet.Currency, cancellationToken);

        return Result.Success(new LedgerPageResponse
        {
            WalletId = wallet.Id.Value,
            Entries = [.. page.Select(entry => FinancialMapper.ToResponse(entry, symbol))],
            NextCursor = hasMore && page.Count > 0 ? page[^1].Sequence : null,
            HasMore = hasMore
        });
    }
}
