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
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Modules.Financial.Infrastructure.Payouts;
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
    private readonly PayoutRouteResolver _routes;
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
        PayoutRouteResolver routes,
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
        _routes = routes;
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
            // **Two refusals, because they are two different problems for the member.**
            //
            // A pending account has simply not been verified yet, and that is now the ordinary
            // state: wallets open active so anybody can be paid, and verification is the gate on
            // taking money out. Answering `account.restricted` sent that member the support-ticket
            // message — "your account is under review, so payments are paused" — for something
            // they can finish themselves in a few minutes, and the fallback sentence
            // ("this account cannot move money out right now") named no cause and no remedy.
            //
            // A member holding cleared earnings they cannot withdraw is exactly who reads this, so
            // it has to say what to do.
            if (account.Status == FinancialAccountStatus.Pending)
            {
                return Result.Failure<WithdrawalResponse>(Error.Forbidden(
                    FinancialErrorCodes.AccountNotEligible,
                    "Verify your identity before moving money out of Diaspora. "
                    + "Your balance is safe in the meantime and you can keep receiving."));
            }

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

        // Priced once, here, and used for both the routing decision and the stored withdrawal. The
        // fee is the platform's and never leaves; what a provider is asked to move is the net, so
        // that is the figure its limits apply to. Routing on the gross would refuse a payout the
        // provider would have accepted, and accept one it would refuse, in each case by the fee.
        var fee = limits.FeeOn(gross);
        var net = gross.Subtract(fee);

        // Can anybody actually carry this? Asked before the balance is checked, because telling a
        // member they have insufficient funds for a payout the platform could never have made is
        // the wrong refusal twice over.
        //
        // Every input is server-held: the country and the rail come off the verified destination
        // rather than off the request. The command carries its own CountryCode — the limits resolver
        // above uses it — and routing on it would let a caller pick the rail their money left on by
        // typing a different country.
        //
        // The payee's verification is derived rather than assumed: a personal account reaches Active
        // through exactly one path — VerificationApprovedConsumer, on a non-partial Trust approval —
        // and CanDebit is that status. The guard above has already refused if it were false, so it
        // is true here today; it is passed as a value anyway so that reordering these checks cannot
        // quietly turn a provider's verification requirement into a lie.
        var unroutable = await RouteRefusalAsync(
            destination, net, caller.Value, account.CanDebit, cancellationToken);

        if (unroutable is not null)
        {
            return Result.Failure<WithdrawalResponse>(unroutable);
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
            // The same figure the route was chosen against, so the provider's limits and the
            // member's receipt cannot disagree.
            fee,
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

    /// <summary>
    /// Asks the router whether anything can carry this payout, and turns a refusal into the error
    /// the member sees.
    /// <para>
    /// <b>Where the verification signal comes from, because there are two and only one of them
    /// means anything here.</b> The fraud assessor's <c>IsVerified</c> is populated from a
    /// <em>permission</em> and says nothing about whether anybody checked a document; passing that
    /// through would let a permission grant satisfy a provider's KYC requirement. The account's
    /// status is the real record: a personal account opens <c>Pending</c> and reaches
    /// <c>Active</c> through exactly one path — <c>VerificationApprovedConsumer</c>, on a
    /// non-partial Trust approval — and <c>VerificationRevokedConsumer</c> restricts it again when
    /// Trust withdraws. That is an event-sourced verification record the platform already keeps,
    /// and it is what a route's <c>RequiresVerifiedPayee</c> is answered with.
    /// </para>
    /// </summary>
    private async Task<Error?> RouteRefusalAsync(
        PayoutDestination destination,
        Money gross,
        Guid payeeUserId,
        bool payeeVerified,
        CancellationToken cancellationToken)
    {
        var result = await _routes.ResolveAsync(new PayoutRouteRequest
        {
            // Off the verified destination, never off the request body — see the call site.
            Country = destination.CountryCode ?? string.Empty,
            Method = destination.Kind,
            Amount = gross,
            PayeeVerified = payeeVerified,

            // A destination bound to a provider can only go to that provider - its token means
            // nothing to any other.
            RequiredProvider = destination.Provider
        },
        payeeUserId,
        cancellationToken);

        if (result.IsRouted)
        {
            return null;
        }

        var code = result.Refusal.ToErrorCode();
        var country = destination.CountryCode ?? "that country";

        return result.Refusal switch
        {
            PayoutRefusalReason.MethodUnsupported => Error.ServiceUnavailable(
                code,
                $"We cannot pay out to that kind of destination in {country} yet. "
                + "Another destination type may work."),

            PayoutRefusalReason.CurrencyUnsupported => Error.ServiceUnavailable(
                code, $"We cannot send {gross.Currency} to {country} yet."),

            PayoutRefusalReason.AmountOutOfRange => Error.Validation(
                code, $"That amount is outside what we can send to {country} in one payout."),

            PayoutRefusalReason.PayeeVerificationRequired => Error.Conflict(
                code, $"Payouts to {country} need identity verification first."),

            PayoutRefusalReason.DestinationProviderUnavailable => Error.ServiceUnavailable(
                code,
                "That saved payout method can no longer be used. Adding a new one should work."),

            PayoutRefusalReason.ProviderOnboardingRequired => Error.Conflict(
                code,
                "The payout provider needs you to finish setting up your account with them "
                + "before it can send money."),

            PayoutRefusalReason.RouteDisabled => Error.ServiceUnavailable(
                code, $"Payouts to {country} are switched off at the moment."),

            // The remaining reasons are the platform's own gaps rather than the member's, and none
            // of them is worth describing to the member in any more detail than this. The code
            // tells an operator which one it was.
            _ => Error.ServiceUnavailable(code, $"We cannot send money to {country} yet.")
        };
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
