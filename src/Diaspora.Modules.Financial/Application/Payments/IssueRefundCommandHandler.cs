using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>Refunds a captured payment.</summary>
public sealed class IssueRefundCommandHandler
    : ICommandHandler<IssueRefundCommand, PaymentIntentResponse>
{
    private readonly IPaymentIntentRepository _intents;
    private readonly PaymentProcessor _processor;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IUnitOfWork _unitOfWork;

    public IssueRefundCommandHandler(
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
        IssueRefundCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var intent = await _intents.FindAsync(
            PaymentIntentId.From(command.PaymentIntentId), cancellationToken);

        if (intent is null)
        {
            return Result.Failure<PaymentIntentResponse>(Error.NotFound(
                FinancialErrorCodes.PaymentIntentNotFound, "That payment does not exist."));
        }

        if (!Enum.TryParse<RefundReason>(command.Reason, true, out var reason))
        {
            return Result.Failure<PaymentIntentResponse>(Error.Validation(
                FinancialErrorCodes.RefundNotAllowed,
                $"'{command.Reason}' is not a refund reason the platform records."));
        }

        var facts = await _currencies.FactsAsync(intent.Currency, cancellationToken);

        var amount = command.Amount is { } value
            ? Money.FromAmount(value, facts.Code, facts.Exponent)
            : intent.RefundableRemaining;

        if (amount.IsZero)
        {
            return Result.Failure<PaymentIntentResponse>(Error.Conflict(
                FinancialErrorCodes.RefundExceedsCaptured,
                "There is nothing left to refund on this payment."));
        }

        Result<PaymentIntent> refunded;

        try
        {
            refunded = await _processor.RefundAsync(
                intent,
                amount,
                reason,
                _authorization.UserIdOrNull(),
                command.Note,
                cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PaymentIntentResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        if (refunded.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(refunded.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(refunded.Value, facts.Symbol));
    }
}
