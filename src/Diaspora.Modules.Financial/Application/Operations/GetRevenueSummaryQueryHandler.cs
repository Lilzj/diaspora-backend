using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Revenue;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>Reads the revenue rollups.</summary>
public sealed class GetRevenueSummaryQueryHandler
    : IQueryHandler<GetRevenueSummaryQuery, IReadOnlyList<RevenueSummaryResponse>>
{
    private readonly IRevenueRecordRepository _revenue;
    private readonly CurrencyCatalog _currencies;

    public GetRevenueSummaryQueryHandler(
        IRevenueRecordRepository revenue, CurrencyCatalog currencies)
    {
        _revenue = revenue;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<RevenueSummaryResponse>>> Handle(
        GetRevenueSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dimension = Enum.TryParse<RevenueDimension>(query.Dimension, true, out var wanted)
            ? wanted
            : RevenueDimension.Total;

        var records = await _revenue.ListAsync(
            query.From, query.To, dimension, query.Currency, cancellationToken);

        var facts = await _currencies.AllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RevenueSummaryResponse>>(
        [
            .. records.Select(record => FinancialMapper.ToResponse(
                record, facts.TryGetValue(record.Currency, out var currency) ? currency.Symbol : null))
        ]);
    }
}
