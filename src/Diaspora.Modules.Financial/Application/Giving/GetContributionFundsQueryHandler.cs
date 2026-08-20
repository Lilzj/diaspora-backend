using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Giving;

/// <summary>Reads the active funds.</summary>
public sealed class GetContributionFundsQueryHandler
    : IQueryHandler<GetContributionFundsQuery, IReadOnlyList<ContributionFundResponse>>
{
    private readonly IContributionFundRepository _funds;
    private readonly CurrencyCatalog _currencies;

    public GetContributionFundsQueryHandler(
        IContributionFundRepository funds, CurrencyCatalog currencies)
    {
        _funds = funds;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<ContributionFundResponse>>> Handle(
        GetContributionFundsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var funds = await _funds.ListActiveAsync(query.CountryCode, cancellationToken);
        var facts = await _currencies.AllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ContributionFundResponse>>(
        [
            .. funds.Select(fund => FinancialMapper.ToResponse(
                fund, facts.TryGetValue(fund.Currency, out var currency) ? currency.Symbol : null))
        ]);
    }
}
