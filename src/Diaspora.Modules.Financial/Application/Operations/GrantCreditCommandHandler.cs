using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// Grants credit and posts the entries that make it a balance.
/// <para>
/// <b>Bounded since 7 Nov 2026.</b> Nobody grants credit to their own account; a grant says why; no
/// grant exceeds <see cref="CreditGrantPolicy.MaximumMajorUnits"/>; and above
/// <see cref="CreditGrantPolicy.ApprovalThresholdMajorUnits"/> this route refuses and the grant goes
/// through a grant request a second Admin decides, as reward points have since RWD-10.
/// </para>
/// <para>
/// The money itself moves in <see cref="CreditGrantIssuer"/>, which the approval of a grant request
/// uses too.
/// </para>
/// </summary>
public sealed class GrantCreditCommandHandler
    : ICommandHandler<GrantCreditCommand, CreditGrantResponse>
{
    private readonly CreditGrantIssuer _issuer;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public GrantCreditCommandHandler(
        CreditGrantIssuer issuer,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _issuer = issuer;
        _authorization = authorization;
        _currencies = currencies;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreditGrantResponse>> Handle(
        GrantCreditCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _authorization.RequireUserId();

        if (staff.IsFailure)
        {
            return Result.Failure<CreditGrantResponse>(staff.Error);
        }

        if (CreditGrantPolicy.RefuseSelf(staff.Value, command.UserId) is { } self)
        {
            return Result.Failure<CreditGrantResponse>(self);
        }

        if (CreditGrantPolicy.RefuseReason(command.Reason) is { } unexplained)
        {
            return Result.Failure<CreditGrantResponse>(unexplained);
        }

        if (CreditGrantPolicy.RefuseAmount(command.Amount) is { } tooMuch)
        {
            return Result.Failure<CreditGrantResponse>(tooMuch);
        }

        if (CreditGrantPolicy.NeedsApproval(command.Amount))
        {
            return Result.Failure<CreditGrantResponse>(Error.Conflict(
                FinancialErrorCodes.CreditGrantNeedsApproval,
                $"Grants above {CreditGrantPolicy.ApprovalThresholdMajorUnits:0} need a second Admin: "
                + "raise a credit grant request."));
        }

        var facts = await _currencies.FactsAsync(command.Currency, cancellationToken);

        if (!await _currencies.IsSupportedAsync(command.Currency, cancellationToken))
        {
            return Result.Failure<CreditGrantResponse>(Error.Validation(
                FinancialErrorCodes.CurrencyNotSupported,
                $"'{command.Currency}' is not a currency this deployment uses."));
        }

        if (!Enum.TryParse<CreditKind>(command.Kind, true, out var kind) || !Enum.IsDefined(kind))
        {
            return Result.Failure<CreditGrantResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid,
                $"'{command.Kind}' is not a kind of credit the platform records."));
        }

        var amount = Money.FromAmount(command.Amount, facts.Code, facts.Exponent);

        if (amount.IsZero)
        {
            return Result.Failure<CreditGrantResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid, "A grant must be for more than nothing."));
        }

        var issued = await _issuer.IssueAsync(
            command.UserId,
            amount,
            kind,
            command.Reason.Trim(),
            command.ExpiresAt,
            grantedByUserId: staff.Value,
            approvedByUserId: null,
            _clock.UtcNow,
            cancellationToken);

        if (issued.IsFailure)
        {
            return Result.Failure<CreditGrantResponse>(issued.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(issued.Value, facts.Symbol));
    }
}
