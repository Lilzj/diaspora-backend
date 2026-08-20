using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Purchases;

/// <summary>Reads the caller's purchase history.</summary>
public sealed class GetMyPurchasesQueryHandler
    : IQueryHandler<GetMyPurchasesQuery, IReadOnlyList<MonetizationPurchaseResponse>>
{
    private readonly IMonetizationPurchaseRepository _purchases;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;

    public GetMyPurchasesQueryHandler(
        IMonetizationPurchaseRepository purchases,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IClock clock)
    {
        _purchases = purchases;
        _authorization = authorization;
        _currencies = currencies;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<MonetizationPurchaseResponse>>> Handle(
        GetMyPurchasesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MonetizationPurchaseResponse>>(caller.Error);
        }

        var status = Enum.TryParse<PurchaseStatus>(query.Status, true, out var wanted)
            ? wanted
            : (PurchaseStatus?)null;

        var purchases = await _purchases.ListByBuyerAsync(
            caller.Value, status, query.Before, query.ExcludeIds, query.Take, cancellationToken);

        var facts = await _currencies.AllAsync(cancellationToken);
        var now = _clock.UtcNow;

        return Result.Success<IReadOnlyList<MonetizationPurchaseResponse>>(
        [
            .. purchases.Select(purchase => FinancialMapper.ToResponse(
                purchase,
                asOf: now,
                symbol: facts.TryGetValue(purchase.Currency, out var currency)
                    ? currency.Symbol
                    : null))
        ]);
    }
}
