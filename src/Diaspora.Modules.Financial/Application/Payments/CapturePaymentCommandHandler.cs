using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>Captures a payment and posts it.</summary>
public sealed class CapturePaymentCommandHandler
    : ICommandHandler<CapturePaymentCommand, PaymentIntentResponse>
{
    private readonly IPaymentIntentRepository _intents;
    private readonly PaymentProcessor _processor;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IUnitOfWork _unitOfWork;

    public CapturePaymentCommandHandler(
        IPaymentIntentRepository intents,
        PaymentProcessor processor,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IUnitOfWork unitOfWork)
    {
        _intents = intents;
        _processor = processor;
        _authorization = authorization;
        _currencies = currencies;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PaymentIntentResponse>> Handle(
        CapturePaymentCommand command, CancellationToken cancellationToken)
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

        var facts = await _currencies.FactsAsync(intent.Currency, cancellationToken);

        var amount = command.Amount is { } value
            ? Money.FromAmount(value, facts.Code, facts.Exponent)
            : null;

        Result<PaymentIntent> captured;

        try
        {
            captured = await _processor.CaptureAsync(intent, amount, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PaymentIntentResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return captured.IsSuccess
            ? Result.Success(FinancialMapper.ToResponse(captured.Value, facts.Symbol))
            : Result.Failure<PaymentIntentResponse>(captured.Error);
    }
}
