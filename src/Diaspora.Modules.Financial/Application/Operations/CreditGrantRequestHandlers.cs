using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Credits;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// Asks for a credit grant that needs a second Admin (7 Nov 2026). Nothing is granted until a second
/// Admin approves.
/// </summary>
public sealed class RequestCreditGrantCommandHandler
    : ICommandHandler<RequestCreditGrantCommand, CreditGrantRequestResponse>
{
    private readonly ICreditGrantRequestRepository _requests;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RequestCreditGrantCommandHandler(
        ICreditGrantRequestRepository requests,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _authorization = authorization;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreditGrantRequestResponse>> Handle(
        RequestCreditGrantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var asker = _authorization.RequireUserId();

        if (asker.IsFailure)
        {
            return Result.Failure<CreditGrantRequestResponse>(asker.Error);
        }

        if (CreditGrantPolicy.RefuseSelf(asker.Value, command.UserId) is { } self)
        {
            return Result.Failure<CreditGrantRequestResponse>(self);
        }

        if (CreditGrantPolicy.RefuseReason(command.Reason) is { } unexplained)
        {
            return Result.Failure<CreditGrantRequestResponse>(unexplained);
        }

        if (CreditGrantPolicy.RefuseAmount(command.Amount) is { } tooMuch)
        {
            return Result.Failure<CreditGrantRequestResponse>(tooMuch);
        }

        if (!await _currencies.IsSupportedAsync(command.Currency, cancellationToken))
        {
            return Result.Failure<CreditGrantRequestResponse>(Error.Validation(
                FinancialErrorCodes.CurrencyNotSupported,
                $"'{command.Currency}' is not a currency this deployment uses."));
        }

        if (!Enum.TryParse<CreditKind>(command.Kind, true, out var kind) || !Enum.IsDefined(kind))
        {
            return Result.Failure<CreditGrantRequestResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid,
                $"'{command.Kind}' is not a kind of credit the platform records."));
        }

        var facts = await _currencies.FactsAsync(command.Currency, cancellationToken);
        var amount = Money.FromAmount(command.Amount, facts.Code, facts.Exponent);

        if (amount.IsZero)
        {
            return Result.Failure<CreditGrantRequestResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid, "A grant must be for more than nothing."));
        }

        var request = CreditGrantRequest.Raise(
            CreditGrantRequestId.From(_guids.NewSequentialGuid()),
            command.UserId,
            amount,
            kind,
            command.Reason,
            command.ExpiresAt,
            asker.Value,
            _clock.UtcNow);

        _requests.Add(request);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(request, facts.Symbol));
    }
}

/// <summary>
/// Approves, rejects or withdraws a credit grant request (7 Nov 2026).
/// <para>
/// <b>An approval issues the credit here, in this transaction,</b> through the same
/// <see cref="CreditGrantIssuer"/> as a direct grant: the asker as grantor, the approver named on the
/// ledger's audit stamp and on the request.
/// </para>
/// </summary>
public sealed class DecideCreditGrantRequestCommandHandler
    : ICommandHandler<DecideCreditGrantRequestCommand, CreditGrantRequestResponse>
{
    private readonly ICreditGrantRequestRepository _requests;
    private readonly CreditGrantIssuer _issuer;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DecideCreditGrantRequestCommandHandler(
        ICreditGrantRequestRepository requests,
        CreditGrantIssuer issuer,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _issuer = issuer;
        _authorization = authorization;
        _currencies = currencies;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreditGrantRequestResponse>> Handle(
        DecideCreditGrantRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var decider = _authorization.RequireUserId();

        if (decider.IsFailure)
        {
            return Result.Failure<CreditGrantRequestResponse>(decider.Error);
        }

        var request = await _requests.FindAsync(
            CreditGrantRequestId.From(command.RequestId), cancellationToken);

        if (request is null)
        {
            return Result.Failure<CreditGrantRequestResponse>(Error.NotFound(
                FinancialErrorCodes.CreditGrantRequestNotFound, "No credit grant request has that id."));
        }

        if (!request.IsPending)
        {
            return Result.Failure<CreditGrantRequestResponse>(Error.Conflict(
                FinancialErrorCodes.CreditGrantRequestDecided,
                $"That credit grant request is already {request.Status.ToString().ToLowerInvariant()}."));
        }

        var now = _clock.UtcNow;
        var asked = request.RequestedByUserId == decider.Value;
        var symbol = await _currencies.SymbolAsync(request.Currency, cancellationToken);

        if (!command.Approve)
        {
            if (string.IsNullOrWhiteSpace(command.Note))
            {
                return Result.Failure<CreditGrantRequestResponse>(Error.Validation(
                    FinancialErrorCodes.CreditGrantDecisionNoteRequired,
                    "Say why the grant is declined."));
            }

            if (asked)
            {
                request.Withdraw(command.Note, now);
            }
            else
            {
                request.Reject(decider.Value, command.Note, now);
            }

            _requests.Update(request);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(FinancialMapperExtensions.ToResponse(request, symbol));
        }

        if (asked)
        {
            return Result.Failure<CreditGrantRequestResponse>(Error.Forbidden(
                FinancialErrorCodes.CreditGrantSelfApproval,
                "Another Admin has to approve a credit grant you asked for. You can withdraw it."));
        }

        if (CreditGrantPolicy.RefuseSelf(decider.Value, request.MemberUserId) is { } self)
        {
            return Result.Failure<CreditGrantRequestResponse>(self);
        }

        var issued = await _issuer.IssueAsync(
            request.MemberUserId,
            request.Amount,
            request.Kind,
            request.Reason,
            request.ExpiresAt,
            grantedByUserId: request.RequestedByUserId,
            approvedByUserId: decider.Value,
            now,
            cancellationToken);

        if (issued.IsFailure)
        {
            return Result.Failure<CreditGrantRequestResponse>(issued.Error);
        }

        request.Approve(decider.Value, command.Note, issued.Value.Id, now);
        _requests.Update(request);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(request, symbol));
    }
}

/// <summary>
/// The credit grant request queue. A separate class from the writes above because DSP4001 refuses a
/// query handler that saves.
/// </summary>
public sealed class GetCreditGrantRequestsQueryHandler
    : IQueryHandler<GetCreditGrantRequestsQuery, IReadOnlyList<CreditGrantRequestResponse>>
{
    private readonly ICreditGrantRequestRepository _requests;
    private readonly CurrencyCatalog _currencies;

    public GetCreditGrantRequestsQueryHandler(
        ICreditGrantRequestRepository requests, CurrencyCatalog currencies)
    {
        _requests = requests;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<CreditGrantRequestResponse>>> Handle(
        GetCreditGrantRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        CreditGrantRequestStatus? status;

        if (string.IsNullOrWhiteSpace(query.State))
        {
            status = CreditGrantRequestStatus.Pending;
        }
        else if (string.Equals(query.State.Trim(), "all", StringComparison.OrdinalIgnoreCase))
        {
            status = null;
        }
        else if (Enum.TryParse<CreditGrantRequestStatus>(query.State.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            status = parsed;
        }
        else
        {
            return Result.Failure<IReadOnlyList<CreditGrantRequestResponse>>(Error.Validation(
                FinancialErrorCodes.CreditGrantRequestStateInvalid,
                "State is Pending, Approved, Rejected, Withdrawn or all."));
        }

        var requests = await _requests.ListAsync(
            status, query.Before, query.ExcludeIds, query.Take, cancellationToken);

        var symbols = await _currencies.AllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CreditGrantRequestResponse>>(
        [
            .. requests.Select(request => FinancialMapperExtensions.ToResponse(
                request,
                symbols.TryGetValue(request.Currency, out var facts) ? facts.Symbol : null))
        ]);
    }
}
