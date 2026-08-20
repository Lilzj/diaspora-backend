using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>Reads one payment.</summary>
public sealed class GetPaymentIntentQueryHandler
    : IQueryHandler<GetPaymentIntentQuery, PaymentIntentResponse>
{
    private readonly IPaymentIntentRepository _intents;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetPaymentIntentQueryHandler(
        IPaymentIntentRepository intents,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _intents = intents;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<PaymentIntentResponse>> Handle(
        GetPaymentIntentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(caller.Error);
        }

        var intent = await _intents.FindAsync(
            PaymentIntentId.From(query.PaymentIntentId), cancellationToken);

        if (intent is null
            || (intent.PayerUserId != caller.Value
                && !_authorization.Has(FinancialPermissions.PaymentsManageAny)))
        {
            return Result.Failure<PaymentIntentResponse>(Error.NotFound(
                FinancialErrorCodes.PaymentIntentNotFound, "That payment does not exist."));
        }

        var symbol = await _currencies.SymbolAsync(intent.Currency, cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(intent, symbol));
    }
}
