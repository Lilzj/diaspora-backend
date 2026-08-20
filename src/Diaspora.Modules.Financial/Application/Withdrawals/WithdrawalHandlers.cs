using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Modules.Financial.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Withdrawals;

/// <summary>
/// Raises a payout request and holds the money behind it.
/// <para>
/// <b>The hold is not a formality — it is what makes two simultaneous withdrawals of the same
/// hundred pounds impossible.</b> The posting refuses a debit the available balance cannot cover, and
/// the wallet's revision is a concurrency token, so the second request either fails its balance check
/// or loses the commit race. Both barriers have to hold; a request that merely recorded an intention
/// would have neither.
/// </para>
/// <para>
/// <b>Refused outright when no settlement provider is registered.</b> No settlement engine ships in
/// this assembly and an architecture test asserts the absence, so a deployment that has not
/// registered one cannot pay anybody. Holding a member's money against a payout that will never
/// happen would be worse than saying no.
/// </para>
/// </summary>
public sealed class CreateWithdrawalCommandHandler
    : ICommandHandler<CreateWithdrawalCommand, WithdrawalResponse>
{
    private readonly IWithdrawalRepository _withdrawals;
    private readonly IPayoutDestinationRepository _destinations;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly FinancialLimitsResolver _limits;
    private readonly WithdrawalProcessor _processor;
    private readonly CurrencyCatalog _currencies;
    private readonly IEnumerable<ISettlementProvider> _settlement;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWithdrawalCommandHandler(
        IWithdrawalRepository withdrawals,
        IPayoutDestinationRepository destinations,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        FinancialLimitsResolver limits,
        WithdrawalProcessor processor,
        CurrencyCatalog currencies,
        IEnumerable<ISettlementProvider> settlement,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _withdrawals = withdrawals;
        _destinations = destinations;
        _identities = identities;
        _authorization = authorization;
        _limits = limits;
        _processor = processor;
        _currencies = currencies;
        _settlement = settlement;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WithdrawalResponse>> Handle(
        CreateWithdrawalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<WithdrawalResponse>(caller.Error);
        }

        var facts = await _currencies.FactsAsync(command.Currency, cancellationToken);

        // Idempotency before the capability check, so a retried request that succeeded once still
        // answers with its withdrawal.
        if (command.IdempotencyKey is { } key
            && await _withdrawals.FindByIdempotencyKeyAsync(caller.Value, key, cancellationToken) is { } existing)
        {
            return Result.Success(FinancialMapperExtensions.ToResponse(existing, facts.Symbol));
        }

        if (!_settlement.Any())
        {
            return Result.Failure<WithdrawalResponse>(Error.ServiceUnavailable(
                FinancialErrorCodes.PayoutNotSupported,
                "This deployment cannot send money out yet."));
        }

        if (command.Amount <= 0)
        {
            return Result.Failure<WithdrawalResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid, "A withdrawal has to be for something."));
        }

        var destination = await _destinations.FindAsync(
            PayoutDestinationId.From(command.DestinationId), cancellationToken);

        // 404 rather than 403 on somebody else's destination: a 403 would confirm it exists, and a
        // payout destination identifier is exactly the sort of thing worth enumerating.
        if (destination is null || destination.OwnerUserId != caller.Value)
        {
            return Result.Failure<WithdrawalResponse>(Error.NotFound(
                FinancialErrorCodes.DestinationNotFound, "That payout destination does not exist."));
        }

        if (!destination.IsUsable)
        {
            return Result.Failure<WithdrawalResponse>(Error.Conflict(
                FinancialErrorCodes.DestinationNotVerified,
                "That destination has not been verified yet."));
        }

        if (!string.Equals(destination.Currency, facts.Code, StringComparison.Ordinal))
        {
            return Result.Failure<WithdrawalResponse>(Error.Validation(
                FinancialErrorCodes.DestinationCurrencyMismatch,
                $"That destination receives {destination.Currency}, not {facts.Code}. "
                + "The platform never converts."));
        }

        var account = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            facts.Code,
            countryCode: command.CountryCode,
            cancellationToken: cancellationToken);

        if (!account.CanDebit)
        {
            return Result.Failure<WithdrawalResponse>(Error.Forbidden(
                FinancialErrorCodes.AccountRestricted,
                account.RestrictionReason ?? "This account cannot move money out right now."));
        }

        var wallet = await _identities.PrimaryWalletAsync(account.Id, facts.Code, cancellationToken);

        if (wallet is null)
        {
            return Result.Failure<WithdrawalResponse>(Error.NotFound(
                FinancialErrorCodes.WalletNotFound,
                $"You hold no {facts.Code} balance to withdraw from."));
        }

        var gross = Money.FromAmount(command.Amount, facts.Code, facts.Exponent);

        var limits = await _limits.ResolveAsync(
            FinancialPolicyKeys.WithdrawalLimits,
            facts.Code,
            command.CountryCode,
            facts.Exponent,
            cancellationToken);

        if (Refuse(gross, limits) is { } refusal)
        {
            return Result.Failure<WithdrawalResponse>(refusal);
        }

        if (limits.DailyMaximum is { } dailyMaximum)
        {
            var usedToday = await _withdrawals.SumSinceAsync(
                caller.Value, facts.Code, _clock.UtcNow.AddDays(-1), cancellationToken);

            if (usedToday + gross.MinorUnits > dailyMaximum.MinorUnits)
            {
                return Result.Failure<WithdrawalResponse>(Error.Conflict(
                    FinancialErrorCodes.WithdrawalDailyLimitReached,
                    "That would take you past what you can withdraw in a day."));
            }
        }

        // The balance check the member sees. The authoritative one is the posting service's, which
        // runs a moment later against the same wallet — this one exists so the refusal names the
        // right thing rather than surfacing as a ledger error.
        if (!wallet.CanDebit(gross))
        {
            return Result.Failure<WithdrawalResponse>(Error.Conflict(
                FinancialErrorCodes.InsufficientFunds,
                $"You have {wallet.Available} available."));
        }

        var withdrawal = Withdrawal.Request(
            WithdrawalId.From(_guids.NewSequentialGuid()),
            caller.Value,
            account.Id,
            wallet.Id,
            destination.Id,
            destination.Label,
            gross,

            // Priced by the server from the policy, and stored — a member told they will receive
            // ninety-eight pounds receives ninety-eight pounds even if the fee changes tomorrow.
            limits.FeeOn(gross),
            _clock.UtcNow,
            command.IdempotencyKey,
            command.CountryCode);

        _withdrawals.Add(withdrawal);

        var held = await _processor.HoldAsync(withdrawal, cancellationToken);

        if (held.IsFailure)
        {
            return Result.Failure<WithdrawalResponse>(held.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        FinancialTelemetry.WalletMovements.Add(
            1,
            new KeyValuePair<string, object?>("operation", "withdrawal"),
            new KeyValuePair<string, object?>("outcome", "requested"));

        return Result.Success(FinancialMapperExtensions.ToResponse(withdrawal, facts.Symbol));
    }

    private static Error? Refuse(Money amount, MovementLimits limits)
    {
        if (limits.Minimum is { } minimum && amount < minimum)
        {
            return Error.Validation(
                FinancialErrorCodes.WithdrawalBelowMinimum,
                $"The smallest withdrawal is {minimum}.");
        }

        return limits.Maximum is { } maximum && amount > maximum
            ? Error.Validation(
                FinancialErrorCodes.WithdrawalAboveMaximum,
                $"The largest single withdrawal is {maximum}.")
            : null;
    }
}

/// <summary>
/// Calls off a withdrawal and gives the money back.
/// <para>
/// Refused once a settlement provider has taken the instruction. Releasing a hold on money that may
/// already be in flight is how a platform pays somebody twice.
/// </para>
/// </summary>
public sealed class CancelWithdrawalCommandHandler
    : ICommandHandler<CancelWithdrawalCommand, WithdrawalResponse>
{
    private readonly IWithdrawalRepository _withdrawals;
    private readonly WithdrawalProcessor _processor;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CancelWithdrawalCommandHandler(
        IWithdrawalRepository withdrawals,
        WithdrawalProcessor processor,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _withdrawals = withdrawals;
        _processor = processor;
        _authorization = authorization;
        _currencies = currencies;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WithdrawalResponse>> Handle(
        CancelWithdrawalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<WithdrawalResponse>(caller.Error);
        }

        var withdrawal = await _withdrawals.FindAsync(
            WithdrawalId.From(command.WithdrawalId), cancellationToken);

        if (withdrawal is null
            || (withdrawal.RequestedByUserId != caller.Value
                && !_authorization.Has(FinancialPermissions.PaymentsManageAny)))
        {
            return Result.Failure<WithdrawalResponse>(Error.NotFound(
                FinancialErrorCodes.WithdrawalNotFound, "That withdrawal does not exist."));
        }

        try
        {
            withdrawal.Cancel(_clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure<WithdrawalResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        var released = await _processor.ReleaseAsync(
            withdrawal, "Cancelled by the member.", cancellationToken);

        if (released.IsFailure)
        {
            return Result.Failure<WithdrawalResponse>(released.Error);
        }

        _withdrawals.Update(withdrawal);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var symbol = await _currencies.SymbolAsync(withdrawal.Currency, cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(withdrawal, symbol));
    }
}
