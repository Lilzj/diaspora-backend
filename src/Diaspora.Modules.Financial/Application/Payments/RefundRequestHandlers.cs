using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>
/// Raises, withdraws, lists and decides member refund requests.
/// <para>
/// <b>Nothing here moves money except the approval branch, and that branch calls the same processor
/// the staff refund route already used.</b> There is one refund path on this platform; this adds a
/// way to ask for one, not a second way to perform one.
/// </para>
/// <para>
/// <b>Three member-choosable reasons, and the other three are refused.</b> <c>Fraudulent</c> and
/// <c>Goodwill</c> are findings a member cannot make about their own payment, and
/// <c>PolicyRule</c> is the platform's. Accepting them from a request body would let a member
/// classify their own refund in a way support reports on.
/// </para>
/// </summary>
public sealed class RefundRequestHandlers
    : ICommandHandler<CreateRefundRequestCommand, RefundRequestResponse>,
        ICommandHandler<WithdrawRefundRequestCommand, RefundRequestResponse>,
        ICommandHandler<DecideRefundRequestCommand, RefundRequestResponse>
{
    /// <summary>What a member may say about their own payment. The rest are the platform's.</summary>
    private static readonly RefundReason[] MemberReasons =
    [
        RefundReason.RequestedByPayer, RefundReason.Duplicate, RefundReason.NotDelivered
    ];

    private readonly IRefundRequestRepository _requests;
    private readonly IPaymentIntentRepository _intents;
    private readonly PaymentProcessor _processor;
    private readonly FinancialAuthorizationService _authorization;
    private readonly FinancialIntegrationEventRelay _relay;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly FinancialOptions _options;

    public RefundRequestHandlers(
        IRefundRequestRepository requests,
        IPaymentIntentRepository intents,
        PaymentProcessor processor,
        FinancialAuthorizationService authorization,
        FinancialIntegrationEventRelay relay,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _requests = requests;
        _intents = intents;
        _processor = processor;
        _authorization = authorization;
        _relay = relay;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Result<RefundRequestResponse>> Handle(
        CreateRefundRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<RefundRequestResponse>(caller.Error);
        }

        if (!_options.EnableRefundRequests)
        {
            return Result.Failure<RefundRequestResponse>(Error.ServiceUnavailable(
                FinancialErrorCodes.RefundNotAllowed,
                "Refund requests are not open on this deployment. Contact support."));
        }

        var intent = await _intents.FindAsync(
            PaymentIntentId.From(command.PaymentIntentId), cancellationToken);

        // 404 rather than 403 on somebody else's payment: a 403 would confirm the payment exists.
        if (intent is null || intent.PayerUserId != caller.Value)
        {
            return Result.Failure<RefundRequestResponse>(Error.NotFound(
                FinancialErrorCodes.PaymentIntentNotFound, "That payment does not exist."));
        }

        if (intent.Status is not (PaymentIntentStatus.Captured or PaymentIntentStatus.Settled))
        {
            return Result.Failure<RefundRequestResponse>(Error.Conflict(
                FinancialErrorCodes.RefundNotAllowed,
                "Only a payment that has actually been taken can be refunded."));
        }

        if (intent.CapturedAt is { } capturedAt
            && _clock.UtcNow > capturedAt.AddDays(Math.Max(1, _options.RefundRequestWindowDays)))
        {
            return Result.Failure<RefundRequestResponse>(Error.Conflict(
                FinancialErrorCodes.RefundWindowClosed,
                $"Refunds can be asked for within {_options.RefundRequestWindowDays} days. "
                + "Contact support."));
        }

        if (await _requests.FindOpenAsync(intent.Id, cancellationToken) is not null)
        {
            return Result.Failure<RefundRequestResponse>(Error.Conflict(
                FinancialErrorCodes.RefundRequestExists,
                "You have already asked about this payment and nobody has answered yet."));
        }

        if (!Enum.TryParse<RefundReason>(command.Reason, true, out var reason)
            || !MemberReasons.Contains(reason))
        {
            return Result.Failure<RefundRequestResponse>(Error.Validation(
                FinancialErrorCodes.RefundNotAllowed,
                $"'{command.Reason}' is not a reason you can give for your own refund."));
        }

        var facts = await _currencies.FactsAsync(intent.Currency, cancellationToken);

        // The whole remaining refundable amount unless the member asked for less. The ceiling is the
        // intent's, never the request's — an amount larger than what was captured is refused here
        // rather than surviving to the approval.
        var amount = command.Amount is { } requested
            ? Money.FromAmount(requested, facts.Code, facts.Exponent)
            : intent.RefundableRemaining;

        if (amount.IsZero || amount > intent.RefundableRemaining)
        {
            return Result.Failure<RefundRequestResponse>(Error.Validation(
                FinancialErrorCodes.RefundExceedsCaptured,
                $"There is {intent.RefundableRemaining} left to refund on that payment."));
        }

        var request = RefundRequest.Raise(
            RefundRequestId.From(_guids.NewSequentialGuid()),
            intent.Id,
            caller.Value,
            amount,
            reason,
            command.Note,
            _clock.UtcNow);

        _requests.Add(request);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _relay.PublishAsync(request, isDecision: false, cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(request, facts.Symbol));
    }

    public async Task<Result<RefundRequestResponse>> Handle(
        WithdrawRefundRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<RefundRequestResponse>(caller.Error);
        }

        var request = await _requests.FindAsync(
            RefundRequestId.From(command.RefundRequestId), cancellationToken);

        if (request is null || request.RequestedByUserId != caller.Value)
        {
            return Result.Failure<RefundRequestResponse>(Error.NotFound(
                FinancialErrorCodes.RefundRequestNotFound, "That request does not exist."));
        }

        try
        {
            request.Withdraw(_clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure<RefundRequestResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        _requests.Update(request);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var symbol = await _currencies.SymbolAsync(request.Currency, cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(request, symbol));
    }

    /// <summary>
    /// Answers a request, and issues the refund when the answer is yes.
    /// <para>
    /// The refund goes through the same processor as the staff route, which calls the gateway and
    /// posts the reverse legs. A provider that refuses fails the whole command — the request stays
    /// open, which is the right state, because nobody has actually been refunded.
    /// </para>
    /// </summary>
    public async Task<Result<RefundRequestResponse>> Handle(
        DecideRefundRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<RefundRequestResponse>(caller.Error);
        }

        var request = await _requests.FindAsync(
            RefundRequestId.From(command.RefundRequestId), cancellationToken);

        if (request is null)
        {
            return Result.Failure<RefundRequestResponse>(Error.NotFound(
                FinancialErrorCodes.RefundRequestNotFound, "That request does not exist."));
        }

        // A request the member's appeal reopened is decided by somebody other than whoever refused
        // it (7 Nov 2026): the appeal is a second look.
        if (request.WasRefusedBy(caller.Value))
        {
            return Result.Failure<RefundRequestResponse>(Error.Forbidden(
                FinancialErrorCodes.RefundRequestSameDecider,
                "You turned this request down before the member's appeal reopened it. "
                + "Another member of staff has to decide it."));
        }

        // A refusal says why, in words the member reads (7 Nov 2026). It reaches them in the notice.
        var note = command.DecisionNote?.Trim();

        if (!command.Approve && (note is null || note.Length < ModerationPolicy.MinimumNoteLength))
        {
            return Result.Failure<RefundRequestResponse>(Error.Validation(
                FinancialErrorCodes.RefundDecisionNoteRequired,
                $"Say why the refund is turned down, in at least {ModerationPolicy.MinimumNoteLength} "
                + "characters. The member reads this note."));
        }

        var symbol = await _currencies.SymbolAsync(request.Currency, cancellationToken);

        if (!command.Approve)
        {
            try
            {
                request.Reject(caller.Value, command.DecisionNote, _clock.UtcNow);
            }
            catch (DomainException exception)
            {
                return Result.Failure<RefundRequestResponse>(
                    Error.Conflict(exception.Code, exception.Message));
            }

            _requests.Update(request);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _relay.PublishAsync(request, isDecision: true, cancellationToken);

            return Result.Success(FinancialMapperExtensions.ToResponse(request, symbol));
        }

        var intent = await _intents.FindAsync(request.PaymentIntentId, cancellationToken);

        if (intent is null)
        {
            return Result.Failure<RefundRequestResponse>(Error.NotFound(
                FinancialErrorCodes.PaymentIntentNotFound,
                "The payment behind that request no longer exists."));
        }

        try
        {
            request.Approve(caller.Value, command.DecisionNote, _clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure<RefundRequestResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        Result<PaymentIntent> refunded;

        try
        {
            refunded = await _processor.RefundAsync(
                intent,
                request.Amount,
                request.Reason,
                caller.Value,
                command.DecisionNote,
                cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result.Failure<RefundRequestResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        if (refunded.IsFailure)
        {
            return Result.Failure<RefundRequestResponse>(refunded.Error);
        }

        if (refunded.Value.Refunds.Count > 0)
        {
            request.AttachRefund(refunded.Value.Refunds[^1].RefundId, _clock.UtcNow);
        }

        _requests.Update(request);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _relay.PublishAsync(request, isDecision: true, cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(request, symbol));
    }
}

/// <summary>
/// Reads over refund requests — the member's own, and the staff queue.
/// <para>
/// A separate class from the writes above because DSP4001 refuses a query handler that saves.
/// </para>
/// </summary>
public sealed class RefundRequestQueryHandlers
    : IQueryHandler<GetMyRefundRequestsQuery, IReadOnlyList<RefundRequestResponse>>,
        IQueryHandler<GetPendingRefundRequestsQuery, IReadOnlyList<RefundRequestResponse>>
{
    private readonly IRefundRequestRepository _requests;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public RefundRequestQueryHandlers(
        IRefundRequestRepository requests,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _requests = requests;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<RefundRequestResponse>>> Handle(
        GetMyRefundRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<RefundRequestResponse>>(caller.Error);
        }

        var requests = await _requests.ListForUserAsync(
            caller.Value, query.Take, cancellationToken);

        return await MapAsync(requests, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<RefundRequestResponse>>> Handle(
        GetPendingRefundRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var requests = await _requests.ListByStatusAsync(
            RefundRequestStatus.Requested, query.Take, cancellationToken);

        return await MapAsync(requests, cancellationToken);
    }

    private async Task<Result<IReadOnlyList<RefundRequestResponse>>> MapAsync(
        IReadOnlyList<RefundRequest> requests, CancellationToken cancellationToken)
    {
        var symbols = await _currencies.AllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RefundRequestResponse>>(
        [
            .. requests.Select(request => FinancialMapperExtensions.ToResponse(
                request,
                symbols.TryGetValue(request.Currency, out var facts) ? facts.Symbol : null))
        ]);
    }
}
