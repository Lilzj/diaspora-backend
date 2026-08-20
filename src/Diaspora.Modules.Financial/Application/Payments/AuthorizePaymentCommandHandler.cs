using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>Authorises a payment through whichever adapter can take it.</summary>
public sealed class AuthorizePaymentCommandHandler
    : ICommandHandler<AuthorizePaymentCommand, PaymentIntentResponse>
{
    private readonly IPaymentIntentRepository _intents;
    private readonly PaymentProcessor _processor;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IUnitOfWork _unitOfWork;

    public AuthorizePaymentCommandHandler(
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
        AuthorizePaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(caller.Error);
        }

        var intent = await _intents.FindAsync(
            PaymentIntentId.From(command.PaymentIntentId), cancellationToken);

        if (intent is null || intent.PayerUserId != caller.Value)
        {
            return Result.Failure<PaymentIntentResponse>(Error.NotFound(
                FinancialErrorCodes.PaymentIntentNotFound, "That payment does not exist."));
        }

        if (intent.HasExpired(DateTimeOffset.MinValue))
        {
            return Result.Failure<PaymentIntentResponse>(Error.Conflict(
                FinancialErrorCodes.PaymentIntentExpired, "That payment has expired. Start again."));
        }

        Result<PaymentIntent> authorized;

        try
        {
            authorized = await _processor.AuthorizeAsync(
                intent, command.ClientContext, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PaymentIntentResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        if (authorized.IsFailure)
        {
            // The failed attempt is still recorded on the aggregate: three declines followed by a
            // success is a different story from one clean success, and it is the story support needs.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<PaymentIntentResponse>(authorized.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var symbol = await _currencies.SymbolAsync(intent.Currency, cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(authorized.Value, symbol));
    }
}
