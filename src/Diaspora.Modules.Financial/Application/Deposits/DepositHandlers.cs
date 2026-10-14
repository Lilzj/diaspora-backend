using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Observability;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Application.Deposits;

/// <summary>
/// Starts a top-up.
/// <para>
/// <b>The order of the checks is the security property.</b> Currency, then idempotency, then the
/// deployment switch, then the policy limits, then the daily ceiling — and only then is an intent
/// created. Every one of those refusals happens before anything exists that a gateway could be asked
/// to charge, so a rejected deposit leaves nothing behind.
/// </para>
/// <para>
/// <b>The wallet is credited on capture, not here.</b> Creating an intent moves no money; the credit
/// leg is posted when the payment actually captures, whether that comes from the client calling
/// capture or from the provider's webhook. A deposit that credited on creation would be a member
/// spending money they had only promised.
/// </para>
/// </summary>
public sealed class CreateDepositCommandHandler
    : ICommandHandler<CreateDepositCommand, PaymentIntentResponse>
{
    private readonly IPaymentIntentRepository _intents;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly FinancialLimitsResolver _limits;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly FinancialOptions _options;

    public CreateDepositCommandHandler(
        IPaymentIntentRepository intents,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        FinancialLimitsResolver limits,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _intents = intents;
        _identities = identities;
        _authorization = authorization;
        _limits = limits;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Result<PaymentIntentResponse>> Handle(
        CreateDepositCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(caller.Error);
        }

        var facts = await _currencies.FactsAsync(command.Currency, cancellationToken);

        if (!await _currencies.IsSupportedAsync(command.Currency, cancellationToken))
        {
            return Result.Failure<PaymentIntentResponse>(Error.Validation(
                FinancialErrorCodes.CurrencyNotSupported,
                $"'{command.Currency}' is not a currency this deployment holds balances in."));
        }

        // Before the switch, so a retried request that succeeded once still answers with its intent
        // even if the capability has since been turned off.
        if (command.IdempotencyKey is { } key
            && await _intents.FindByIdempotencyKeyAsync(caller.Value, key, cancellationToken) is { } existing)
        {
            return Result.Success(FinancialMapper.ToResponse(existing, facts.Symbol));
        }

        if (!_options.EnableDeposits)
        {
            return Result.Failure<PaymentIntentResponse>(Error.ServiceUnavailable(
                FinancialErrorCodes.DepositNotSupported,
                "This deployment does not hold member balances."));
        }

        if (!Enum.TryParse<PaymentMethodKind>(command.MethodKind, true, out var methodKind))
        {
            return Result.Failure<PaymentIntentResponse>(Error.Validation(
                FinancialErrorCodes.PaymentMethodUnsupported,
                $"'{command.MethodKind}' is not a payment method the platform recognises."));
        }

        if (command.Amount <= 0)
        {
            return Result.Failure<PaymentIntentResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid, "A deposit has to be for something."));
        }

        var amount = Money.FromAmount(command.Amount, facts.Code, facts.Exponent);

        var limits = await _limits.ResolveAsync(
            FinancialPolicyKeys.DepositLimits,
            facts.Code,
            command.CountryCode,
            facts.Exponent,
            cancellationToken);

        if (Refuse(amount, limits) is { } refusal)
        {
            return Result.Failure<PaymentIntentResponse>(refusal);
        }

        if (limits.DailyMaximum is { } dailyMaximum)
        {
            var usedToday = await DepositedTodayAsync(caller.Value, facts.Code, cancellationToken);

            if (usedToday + amount.MinorUnits > dailyMaximum.MinorUnits)
            {
                return Result.Failure<PaymentIntentResponse>(Error.Conflict(
                    FinancialErrorCodes.DepositDailyLimitReached,
                    "That would take you past what you can add in a day."));
            }
        }

        var account = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            facts.Code,
            countryCode: command.CountryCode,
            cancellationToken: cancellationToken);

        // A pending account may receive money and may not spend it, which is exactly right for a
        // deposit: somebody can top up before they are verified and cannot move it out until they are.
        if (account.Status == FinancialAccountStatus.Restricted)
        {
            return Result.Failure<PaymentIntentResponse>(Error.Forbidden(
                FinancialErrorCodes.AccountRestricted,
                account.RestrictionReason ?? "This account cannot add money right now."));
        }

        var intent = PaymentIntent.Create(
            PaymentIntentId.From(_guids.NewSequentialGuid()),
            caller.Value,
            account.Id,
            amount,
            methodKind,
            "Wallet top-up",
            _clock.UtcNow,
            subject: null,
            command.IdempotencyKey,
            command.CountryCode,
            purpose: PaymentPurpose.Deposit);

        _intents.Add(intent);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        FinancialTelemetry.WalletMovements.Add(
            1,
            new KeyValuePair<string, object?>("operation", "deposit"),
            new KeyValuePair<string, object?>("outcome", "created"));

        return Result.Success(FinancialMapper.ToResponse(intent, facts.Symbol));
    }

    /// <summary>
    /// What this member has already added today, in one currency.
    /// <para>
    /// Counts everything not terminally failed. A cap that only counted captured deposits would let
    /// somebody open a hundred intents before the first settled.
    /// </para>
    /// </summary>
    private async Task<long> DepositedTodayAsync(
        Guid userId, string currency, CancellationToken cancellationToken)
    {
        var since = _clock.UtcNow.AddDays(-1);

        var recent = await _intents.ListByPayerAsync(
            userId, null, Domain.Policies.FinancialLimits.MaxLedgerPageSize, cancellationToken);

        return recent
            .Where(intent => intent.Purpose == PaymentPurpose.Deposit)
            .Where(intent => string.Equals(intent.Currency, currency, StringComparison.Ordinal))
            .Where(intent => intent.CreatedAt >= since)
            .Where(intent => intent.Status
                is not (PaymentIntentStatus.Failed
                    or PaymentIntentStatus.Cancelled
                    or PaymentIntentStatus.Expired))
            .Sum(intent => intent.AmountMinorUnits);
    }

    private static Error? Refuse(Money amount, MovementLimits limits)
    {
        if (limits.Minimum is { } minimum && amount < minimum)
        {
            return Error.Validation(
                FinancialErrorCodes.DepositBelowMinimum,
                $"The smallest top-up is {minimum}.");
        }

        return limits.Maximum is { } maximum && amount > maximum
            ? Error.Validation(
                FinancialErrorCodes.DepositAboveMaximum,
                $"The largest single top-up is {maximum}.")
            : null;
    }
}

/// <summary>The caller's top-ups, cursor-paged.</summary>
public sealed class GetMyDepositsQueryHandler
    : IQueryHandler<GetMyDepositsQuery, IReadOnlyList<PaymentIntentResponse>>
{
    private readonly IPaymentIntentRepository _intents;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetMyDepositsQueryHandler(
        IPaymentIntentRepository intents,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _intents = intents;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<PaymentIntentResponse>>> Handle(
        GetMyDepositsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<PaymentIntentResponse>>(caller.Error);
        }

        var status = Enum.TryParse<PaymentIntentStatus>(query.Status, true, out var wanted)
            ? wanted
            : (PaymentIntentStatus?)null;

        var deposits = await _intents.ListByPayerAsync(
            caller.Value,
            status,
            PaymentPurpose.Deposit,
            query.Before,
            query.ExcludeIds,
            query.Take,
            cancellationToken);

        var symbols = await _currencies.AllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PaymentIntentResponse>>(
        [
            .. deposits.Select(intent => FinancialMapper.ToResponse(
                intent,
                symbols.TryGetValue(intent.Currency, out var facts) ? facts.Symbol : null))
        ]);
    }
}

/// <summary>What the caller may deposit, and what it would cost them.</summary>
public sealed class GetDepositLimitsQueryHandler
    : IQueryHandler<GetDepositLimitsQuery, MovementLimitsResponse>
{
    private readonly IPaymentIntentRepository _intents;
    private readonly FinancialAuthorizationService _authorization;
    private readonly FinancialLimitsResolver _limits;
    private readonly CurrencyCatalog _currencies;
    private readonly MemberCurrency _memberCurrency;
    private readonly IClock _clock;
    private readonly FinancialOptions _options;

    public GetDepositLimitsQueryHandler(
        IPaymentIntentRepository intents,
        FinancialAuthorizationService authorization,
        FinancialLimitsResolver limits,
        CurrencyCatalog currencies,
        MemberCurrency memberCurrency,
        IClock clock,
        IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _intents = intents;
        _authorization = authorization;
        _limits = limits;
        _currencies = currencies;
        _memberCurrency = memberCurrency;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<Result<MovementLimitsResponse>> Handle(
        GetDepositLimitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<MovementLimitsResponse>(caller.Error);
        }

        // Limits are quoted in the caller's currency — the money they hold, else where they live —
        // unless they asked about another. The platform default is only the last resort.
        var currency = query.Currency
            ?? await _memberCurrency.ForMemberAsync(caller.Value, cancellationToken);
        var facts = await _currencies.FactsAsync(currency, cancellationToken);

        var limits = await _limits.ResolveAsync(
            FinancialPolicyKeys.DepositLimits,
            facts.Code,
            query.CountryCode,
            facts.Exponent,
            cancellationToken);

        var since = _clock.UtcNow.AddDays(-1);

        var recent = await _intents.ListByPayerAsync(
            caller.Value, null, Domain.Policies.FinancialLimits.MaxLedgerPageSize, cancellationToken);

        var usedToday = recent
            .Where(intent => intent.Purpose == PaymentPurpose.Deposit)
            .Where(intent => string.Equals(intent.Currency, facts.Code, StringComparison.Ordinal))
            .Where(intent => intent.CreatedAt >= since)
            .Where(intent => intent.Status
                is not (PaymentIntentStatus.Failed
                    or PaymentIntentStatus.Cancelled
                    or PaymentIntentStatus.Expired))
            .Sum(intent => intent.AmountMinorUnits);

        return Result.Success(FinancialMapperExtensions.ToResponse(
            limits,
            _options.EnableDeposits,
            _options.EnableDeposits ? null : FinancialErrorCodes.DepositNotSupported,
            usedToday,
            available: null,
            facts.Symbol));
    }
}
