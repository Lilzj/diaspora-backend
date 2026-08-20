using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>Cancels a payment that has not been captured.</summary>
public sealed class CancelPaymentCommandHandler
    : ICommandHandler<CancelPaymentCommand, PaymentIntentResponse>
{
    private readonly IPaymentIntentRepository _intents;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPaymentCommandHandler(
        IPaymentIntentRepository intents,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _intents = intents;
        _authorization = authorization;
        _currencies = currencies;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PaymentIntentResponse>> Handle(
        CancelPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(caller.Error);
        }

        var intent = await _intents.FindAsync(
            PaymentIntentId.From(command.PaymentIntentId), cancellationToken);

        if (intent is null
            || (intent.PayerUserId != caller.Value
                && !_authorization.Has(FinancialPermissions.PaymentsManageAny)))
        {
            return Result.Failure<PaymentIntentResponse>(Error.NotFound(
                FinancialErrorCodes.PaymentIntentNotFound, "That payment does not exist."));
        }

        try
        {
            intent.Cancel(_clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PaymentIntentResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        _intents.Update(intent);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var symbol = await _currencies.SymbolAsync(intent.Currency, cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(intent, symbol));
    }
}
