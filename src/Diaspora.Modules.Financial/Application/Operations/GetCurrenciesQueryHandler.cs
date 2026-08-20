using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Currencies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>Reads the currency catalogue.</summary>
public sealed class GetCurrenciesQueryHandler
    : IQueryHandler<GetCurrenciesQuery, IReadOnlyList<CurrencyResponse>>
{
    private readonly ICurrencyRepository _currencies;

    public GetCurrenciesQueryHandler(ICurrencyRepository currencies) => _currencies = currencies;

    public async Task<Result<IReadOnlyList<CurrencyResponse>>> Handle(
        GetCurrenciesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var currencies = await _currencies.ListActiveAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CurrencyResponse>>(
            [.. currencies.Select(FinancialMapper.ToResponse)]);
    }
}
