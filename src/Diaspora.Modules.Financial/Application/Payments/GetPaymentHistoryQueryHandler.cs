using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>Reads the caller's payment history.</summary>
public sealed class GetPaymentHistoryQueryHandler
    : IQueryHandler<GetPaymentHistoryQuery, IReadOnlyList<PaymentIntentResponse>>
{
    private readonly IPaymentIntentRepository _intents;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetPaymentHistoryQueryHandler(
        IPaymentIntentRepository intents,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _intents = intents;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<PaymentIntentResponse>>> Handle(
        GetPaymentHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<PaymentIntentResponse>>(caller.Error);
        }

        var status = Enum.TryParse<PaymentIntentStatus>(query.Status, true, out var wanted)
            ? wanted
            : (PaymentIntentStatus?)null;

        var intents = await _intents.ListByPayerAsync(
            caller.Value,
            status,
            purpose: null,
            query.Before,
            query.ExcludeIds,
            query.Take,
            cancellationToken);

        var facts = await _currencies.AllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PaymentIntentResponse>>(
        [
            .. intents.Select(intent => FinancialMapper.ToResponse(
                intent,
                facts.TryGetValue(intent.Currency, out var currency) ? currency.Symbol : null))
        ]);
    }
}
