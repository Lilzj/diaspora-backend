using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Currencies;
using Diaspora.Modules.Financial.Extensibility.Currency;
using Diaspora.Modules.Financial.Extensibility.Fraud;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Modules.Financial.Extensibility.Search;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Modules.Financial.Infrastructure.Fraud;
using Diaspora.Modules.Financial.Infrastructure.Search;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// Reports what is registered and what is not.
/// <para>
/// <b>The three "no" answers are the useful ones.</b> Currency conversion is off because no
/// <c>IExchangeRateProvider</c> ships; payouts are off because no settlement provider does; search
/// projection is off while the no-op projector is the only one registered. Saying so is a great deal
/// more useful than an empty result somebody reads as "no data".
/// </para>
/// </summary>
public sealed class GetFinancialCapabilitiesQueryHandler
    : IQueryHandler<GetFinancialCapabilitiesQuery, FinancialCapabilitiesResponse>
{
    private readonly IPaymentGatewayRegistry _gateways;
    private readonly ICurrencyRepository _currencies;
    private readonly IEnumerable<IExchangeRateProvider> _rates;
    private readonly IEnumerable<ISettlementProvider> _settlement;
    private readonly IEnumerable<IFraudAssessor> _fraud;
    private readonly IFinancialSearchProjector _search;

    public GetFinancialCapabilitiesQueryHandler(
        IPaymentGatewayRegistry gateways,
        ICurrencyRepository currencies,
        IEnumerable<IExchangeRateProvider> rates,
        IEnumerable<ISettlementProvider> settlement,
        IEnumerable<IFraudAssessor> fraud,
        IFinancialSearchProjector search)
    {
        _gateways = gateways;
        _currencies = currencies;
        _rates = rates;
        _settlement = settlement;
        _fraud = fraud;
        _search = search;
    }

    public async Task<Result<FinancialCapabilitiesResponse>> Handle(
        GetFinancialCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var currencies = await _currencies.ListActiveAsync(cancellationToken);

        return Result.Success(new FinancialCapabilitiesResponse
        {
            Gateways = [.. _gateways.All.Select(gateway => gateway.Provider).Order(StringComparer.Ordinal)],
            SupportedMethods =
            [
                .. _gateways.All
                    .SelectMany(gateway => gateway.SupportedMethods)
                    .Distinct()
                    .Select(method => method.ToString())
                    .Order(StringComparer.Ordinal)
            ],
            Currencies = [.. currencies.Select(FinancialMapper.ToResponse)],
            CurrencyConversionEnabled = _rates.Any(),
            PayoutsEnabled = _settlement.Any(),

            // The shipped velocity assessor does not count: the question is whether anything beyond
            // it is registered.
            FraudAssessmentEnabled = _fraud.Any(assessor => assessor is not VelocityFraudAssessor),
            SearchProjectionEnabled = _search is not NoOpFinancialSearchProjector
        });
    }
}
