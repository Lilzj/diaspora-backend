using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>
/// Reads the caller's wallets.
/// <para>
/// <b>An empty list is a legitimate answer and is not an error.</b> A member who has never
/// transacted has no financial identity yet — one is opened when Trust approves their verification —
/// and answering 404 would make "no wallet" indistinguishable from "wrong endpoint".
/// </para>
/// <para>
/// <b>The platform's own wallets are never returned here.</b> Clearing, revenue and escrow are
/// nominal accounts; the clearing one runs negative by construction, and serving it to a member's
/// wallet screen would be serving a number that means the opposite of what it looks like.
/// </para>
/// </summary>
public sealed class GetMyWalletQueryHandler
    : IQueryHandler<GetMyWalletQuery, IReadOnlyList<WalletResponse>>
{
    private readonly IWalletRepository _wallets;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetMyWalletQueryHandler(
        IWalletRepository wallets,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _wallets = wallets;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<WalletResponse>>> Handle(
        GetMyWalletQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<WalletResponse>>(caller.Error);
        }

        var wallets = await _wallets.ListByOwnerAsync(caller.Value, cancellationToken);
        var facts = await _currencies.AllAsync(cancellationToken);

        var visible = wallets
            .Where(wallet => wallet.OwnerType != FinancialAccountType.Platform)
            .Where(wallet => query.Currency is null
                || string.Equals(
                    wallet.Currency,
                    query.Currency.Trim().ToUpperInvariant(),
                    StringComparison.Ordinal))
            .Select(wallet => FinancialMapper.ToResponse(
                wallet,
                facts.TryGetValue(wallet.Currency, out var currency) ? currency.Symbol : null))
            .ToList();

        return Result.Success<IReadOnlyList<WalletResponse>>(visible);
    }
}
