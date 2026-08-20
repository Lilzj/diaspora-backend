using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Subscriptions;

/// <summary>Reads the plan catalogue.</summary>
public sealed class GetSubscriptionPlansQueryHandler
    : IQueryHandler<GetSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanResponse>>
{
    private readonly ISubscriptionPlanRepository _plans;
    private readonly CurrencyCatalog _currencies;

    public GetSubscriptionPlansQueryHandler(
        ISubscriptionPlanRepository plans, CurrencyCatalog currencies)
    {
        _plans = plans;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<SubscriptionPlanResponse>>> Handle(
        GetSubscriptionPlansQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var audience = Enum.TryParse<FinancialAccountType>(query.AudienceType, true, out var wanted)
            ? wanted
            : (FinancialAccountType?)null;

        var plans = await _plans.ListActiveAsync(audience, query.CountryCode, cancellationToken);
        var facts = await _currencies.AllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SubscriptionPlanResponse>>(
        [
            .. plans.Select(plan => FinancialMapper.ToResponse(
                plan, facts.TryGetValue(plan.Currency, out var currency) ? currency.Symbol : null))
        ]);
    }
}
