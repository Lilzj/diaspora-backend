using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Subscriptions;

/// <summary>
/// Prices a plan change, from policy and nowhere else.
/// <para>
/// <b>One computation, read by both the preview and the change.</b> Two code paths computing one
/// proration is how a preview and a charge come to disagree, and the disagreement is only ever
/// noticed by the member it overcharged.
/// </para>
/// <para>
/// <b>The credit is the unused fraction of what was paid, rounded down.</b> Rounding a credit up
/// would mean the platform giving away a minor unit on every change; rounding the charge down means
/// the residual stays with the member, which is the direction <c>Money.MultiplyBy</c> already
/// documents and which must not be chosen twice.
/// </para>
/// </summary>
public sealed class SubscriptionChangePricer
{
    private readonly FinancialLimitsResolver _limits;

    public SubscriptionChangePricer(FinancialLimitsResolver limits) => _limits = limits;

    public async Task<PlanChange> PriceAsync(
        Subscription subscription,
        SubscriptionPlan from,
        SubscriptionPlan to,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var policy = await _limits.MostSpecificAsync(
            FinancialPolicyKeys.SubscriptionProration, to.CountryCode, cancellationToken);

        var prorateUpgrade =
            policy?.Parameter(FinancialPolicyParameters.ProrateUpgrade) is not "false";

        var deferDowngrade =
            policy?.Parameter(FinancialPolicyParameters.DeferDowngrade) is not "false";

        var kind = Classify(from, to);

        // A cross-currency change is refused rather than converted. The platform never converts, and
        // a proration that did would be the one place on the whole platform that priced money at a
        // rate nobody chose.
        if (!string.Equals(from.Currency, to.Currency, StringComparison.Ordinal))
        {
            return Refused(
                from,
                to,
                kind,
                now,
                FinancialErrorCodes.ConversionNotSupported,
                $"That plan is priced in {to.Currency} and yours in {from.Currency}. "
                + "The platform never converts.",
                policy?.VersionLabel);
        }

        var credit = UnusedCredit(subscription, from, now);
        var isUpgrade = kind == "Upgrade";

        var effectiveAt = isUpgrade || !deferDowngrade
            ? now
            : subscription.CurrentPeriod.EndsAt ?? now;

        // A downgrade charges nothing now: the member keeps what they paid for until the boundary.
        // An upgrade charges the difference between the new price and what the old one is still worth.
        var dueNow = isUpgrade && prorateUpgrade
            ? Difference(to.Price, credit)
            : Money.Zero(to.Currency, to.Exponent);

        var gained = to.EntitlementKeys.Except(from.EntitlementKeys, StringComparer.Ordinal);
        var lost = from.EntitlementKeys.Except(to.EntitlementKeys, StringComparer.Ordinal);

        return new PlanChange
        {
            From = from,
            To = to,
            Kind = kind,
            DueNow = dueNow,
            ProratedCredit = credit,
            NextChargeAmount = to.Price,
            NextChargeAt = effectiveAt == now
                ? to.NextPeriodEnd(now)
                : subscription.CurrentPeriod.EndsAt,
            EffectiveAt = effectiveAt,
            GainedEntitlementKeys = [.. gained],
            LostEntitlementKeys = [.. lost],
            IsPermitted = true,
            PolicyVersion = policy?.VersionLabel
        };
    }

    private static string Classify(SubscriptionPlan from, SubscriptionPlan to)
    {
        if (from.Interval != to.Interval && from.Tier == to.Tier)
        {
            return "IntervalChange";
        }

        return to.PriceMinorUnits > from.PriceMinorUnits ? "Upgrade" : "Downgrade";
    }

    /// <summary>
    /// What the unused remainder of the current period is worth.
    /// <para>
    /// Rounded down, so the platform never credits a minor unit it did not receive. Zero once the
    /// period has ended, and zero on a trial — a trial cost nothing and is worth nothing back.
    /// </para>
    /// </summary>
    private static Money UnusedCredit(
        Subscription subscription, SubscriptionPlan from, DateTimeOffset now)
    {
        var zero = Money.Zero(from.Currency, from.Exponent);

        if (subscription.Status == SubscriptionStatus.Trial
            || subscription.CurrentPeriod.EndsAt is not { } endsAt
            || endsAt <= now)
        {
            return zero;
        }

        var total = endsAt - subscription.CurrentPeriod.StartsAt;

        if (total <= TimeSpan.Zero)
        {
            return zero;
        }

        var remaining = endsAt - now;
        var fraction = (decimal)(remaining.TotalSeconds / total.TotalSeconds);

        return Money.FromMinorUnits(
            (long)Math.Floor(from.PriceMinorUnits * Math.Clamp(fraction, 0m, 1m)),
            from.Currency,
            from.Exponent);
    }

    private static Money Difference(Money price, Money credit) =>
        credit.MinorUnits >= price.MinorUnits
            ? Money.Zero(price.Currency, price.Exponent)
            : price.Subtract(credit);

    private static PlanChange Refused(
        SubscriptionPlan from,
        SubscriptionPlan to,
        string kind,
        DateTimeOffset now,
        string refusalCode,
        string refusalMessage,
        string? policyVersion) =>
        new()
        {
            From = from,
            To = to,
            Kind = kind,
            DueNow = Money.Zero(to.Currency, to.Exponent),
            ProratedCredit = Money.Zero(to.Currency, to.Exponent),
            NextChargeAmount = to.Price,
            EffectiveAt = now,
            GainedEntitlementKeys = [],
            LostEntitlementKeys = [],
            IsPermitted = false,
            RefusalCode = refusalCode,
            RefusalMessage = refusalMessage,
            PolicyVersion = policyVersion
        };
}

/// <summary>A priced plan change.</summary>
public sealed record PlanChange
{
    public required SubscriptionPlan From { get; init; }

    public required SubscriptionPlan To { get; init; }

    public required string Kind { get; init; }

    public required Money DueNow { get; init; }

    public required Money ProratedCredit { get; init; }

    public required Money NextChargeAmount { get; init; }

    public DateTimeOffset? NextChargeAt { get; init; }

    public required DateTimeOffset EffectiveAt { get; init; }

    public required IReadOnlyList<string> GainedEntitlementKeys { get; init; }

    public required IReadOnlyList<string> LostEntitlementKeys { get; init; }

    public required bool IsPermitted { get; init; }

    public string? RefusalCode { get; init; }

    public string? RefusalMessage { get; init; }

    public string? PolicyVersion { get; init; }
}

/// <summary>
/// Applies a plan change.
/// <para>
/// The previews live in <see cref="SubscriptionChangeQueryHandlers"/> rather than here: DSP4001
/// refuses a query handler that saves, and a type whose transactional behaviour depends on which
/// method was called is a type nobody can reason about from its signature.
/// </para>
/// </summary>
public sealed class SubscriptionChangeHandlers
    : ICommandHandler<ChangeSubscriptionPlanCommand, SubscriptionResponse>
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly IPaymentIntentRepository _intents;
    private readonly SubscriptionChangePricer _pricer;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SubscriptionChangeHandlers(
        ISubscriptionRepository subscriptions,
        ISubscriptionPlanRepository plans,
        IPaymentIntentRepository intents,
        SubscriptionChangePricer pricer,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _subscriptions = subscriptions;
        _plans = plans;
        _intents = intents;
        _pricer = pricer;
        _identities = identities;
        _authorization = authorization;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Applies the change.
    /// <para>
    /// <b>An upgrade renews the subscription onto the new plan immediately and opens a payment for
    /// the difference.</b> A downgrade records the target and lets the renewal sweep move it at the
    /// period boundary — which is what "somebody who has paid for the month keeps the month" means in
    /// code.
    /// </para>
    /// </summary>
    public async Task<Result<SubscriptionResponse>> Handle(
        ChangeSubscriptionPlanCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var resolved = await ResolveAsync(
            command.SubscriptionId, command.PlanCode, cancellationToken);

        if (resolved.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(resolved.Error);
        }

        var (subscription, from, to) = resolved.Value;
        var now = _clock.UtcNow;
        var change = await _pricer.PriceAsync(subscription, from, to, now, cancellationToken);

        if (!change.IsPermitted)
        {
            FinancialTelemetry.SubscriptionChanges.Add(
                1,
                new KeyValuePair<string, object?>("direction", change.Kind),
                new KeyValuePair<string, object?>("outcome", "refused"));

            return Result.Failure<SubscriptionResponse>(Error.Conflict(
                change.RefusalCode ?? FinancialErrorCodes.SubscriptionChangeNotPermitted,
                change.RefusalMessage ?? "That plan change is not available."));
        }

        if (change.EffectiveAt > now)
        {
            subscription.ScheduleChange(to.Code, change.EffectiveAt, now);
            _subscriptions.Update(subscription);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            FinancialTelemetry.SubscriptionChanges.Add(
                1,
                new KeyValuePair<string, object?>("direction", change.Kind),
                new KeyValuePair<string, object?>("outcome", "scheduled"));

            return Result.Success(FinancialMapper.ToResponse(subscription));
        }

        subscription.Renew(to, now, () => _guids.NewSequentialGuid());
        _subscriptions.Update(subscription);

        if (!change.DueNow.IsZero)
        {
            var account = await _identities.GetOrCreateAsync(
                subscription.SubscriberUserId,
                FinancialAccountType.Personal,
                to.Currency,
                countryCode: command.CountryCode,
                cancellationToken: cancellationToken);

            var methodKind =
                Enum.TryParse<PaymentMethodKind>(command.MethodKind, true, out var parsed)
                    ? parsed
                    : PaymentMethodKind.Card;

            var intent = PaymentIntent.Create(
                PaymentIntentId.From(_guids.NewSequentialGuid()),
                subscription.SubscriberUserId,
                account.Id,
                change.DueNow,
                methodKind,
                $"Upgrade to {to.Name}",
                now,
                SubjectReference.ForMember(subscription.SubscriberUserId),
                command.IdempotencyKey
                    ?? $"subscription-change:{subscription.Id.Value:N}:{to.Code}",
                command.CountryCode);

            _intents.Add(intent);
            subscription.AttachPayment(intent.Id);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        FinancialTelemetry.SubscriptionChanges.Add(
            1,
            new KeyValuePair<string, object?>("direction", change.Kind),
            new KeyValuePair<string, object?>("outcome", "applied"));

        return Result.Success(FinancialMapper.ToResponse(subscription));
    }

    private async Task<Result<(Subscription Subscription, SubscriptionPlan From, SubscriptionPlan To)>>
        ResolveAsync(Guid subscriptionId, string planCode, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<(Subscription, SubscriptionPlan, SubscriptionPlan)>(caller.Error);
        }

        var subscription = await _subscriptions.FindAsync(
            SubscriptionId.From(subscriptionId), cancellationToken);

        // 404 rather than 403 on somebody else's subscription: a 403 confirms it exists.
        if (subscription is null || subscription.SubscriberUserId != caller.Value)
        {
            return Result.Failure<(Subscription, SubscriptionPlan, SubscriptionPlan)>(
                Error.NotFound(
                    FinancialErrorCodes.SubscriptionNotFound,
                    "That subscription does not exist."));
        }

        if (!subscription.GrantsAccess)
        {
            return Result.Failure<(Subscription, SubscriptionPlan, SubscriptionPlan)>(
                Error.Conflict(
                    FinancialErrorCodes.SubscriptionChangeNotPermitted,
                    "That subscription has ended. Start a new one."));
        }

        var from = await _plans.FindByCodeAsync(subscription.PlanCode, cancellationToken);
        var to = await _plans.FindByCodeAsync(planCode, cancellationToken);

        if (from is null || to is null || !to.IsActive)
        {
            return Result.Failure<(Subscription, SubscriptionPlan, SubscriptionPlan)>(
                Error.NotFound(FinancialErrorCodes.PlanNotFound, "There is no such plan."));
        }

        if (string.Equals(from.Code, to.Code, StringComparison.Ordinal))
        {
            return Result.Failure<(Subscription, SubscriptionPlan, SubscriptionPlan)>(
                Error.Conflict(
                    FinancialErrorCodes.SubscriptionSamePlan,
                    "That is the plan you already hold."));
        }

        return Result.Success((subscription, from, to));
    }
}

/// <summary>
/// What a plan change would cost, and what the next charge will be.
/// <para>
/// Both are reads and neither writes, which is what lets them sit in their own type. The pricing is
/// the same <see cref="SubscriptionChangePricer"/> the change itself uses — two computations of one
/// proration is how a preview comes to disagree with a charge, and the disagreement is only ever
/// noticed by the member it overcharged.
/// </para>
/// </summary>
public sealed class SubscriptionChangeQueryHandlers
    : IQueryHandler<PreviewSubscriptionChangeQuery, PlanChangePreviewResponse>,
        IQueryHandler<GetUpcomingChargesQuery, IReadOnlyList<UpcomingChargeResponse>>
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly SubscriptionChangePricer _pricer;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;

    public SubscriptionChangeQueryHandlers(
        ISubscriptionRepository subscriptions,
        ISubscriptionPlanRepository plans,
        SubscriptionChangePricer pricer,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IClock clock)
    {
        _subscriptions = subscriptions;
        _plans = plans;
        _pricer = pricer;
        _authorization = authorization;
        _currencies = currencies;
        _clock = clock;
    }

    public async Task<Result<PlanChangePreviewResponse>> Handle(
        PreviewSubscriptionChangeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var resolved = await ResolveAsync(query.SubscriptionId, query.PlanCode, cancellationToken);

        if (resolved.IsFailure)
        {
            return Result.Failure<PlanChangePreviewResponse>(resolved.Error);
        }

        var (subscription, from, to) = resolved.Value;

        var change = await _pricer.PriceAsync(
            subscription, from, to, _clock.UtcNow, cancellationToken);

        var symbol = await _currencies.SymbolAsync(to.Currency, cancellationToken);

        return Result.Success(new PlanChangePreviewResponse
        {
            FromPlanCode = from.Code,
            ToPlanCode = to.Code,
            Kind = change.Kind,
            DueNow = FinancialMapper.ToResponse(change.DueNow, symbol),
            ProratedCredit = FinancialMapper.ToResponse(change.ProratedCredit, symbol),
            NextChargeAmount = FinancialMapper.ToResponse(change.NextChargeAmount, symbol),
            NextChargeAt = change.NextChargeAt,
            EffectiveAt = change.EffectiveAt,
            GainedEntitlementKeys = change.GainedEntitlementKeys,
            LostEntitlementKeys = change.LostEntitlementKeys,
            IsPermitted = change.IsPermitted,
            RefusalCode = change.RefusalCode,
            RefusalMessage = change.RefusalMessage,
            PolicyVersion = change.PolicyVersion
        });
    }

    public async Task<Result<IReadOnlyList<UpcomingChargeResponse>>> Handle(
        GetUpcomingChargesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<UpcomingChargeResponse>>(caller.Error);
        }

        var subscription = await _subscriptions.FindActiveForSubscriberAsync(
            caller.Value, cancellationToken);

        if (subscription is null || !subscription.GrantsAccess)
        {
            return Result.Success<IReadOnlyList<UpcomingChargeResponse>>([]);
        }

        // The plan that will actually be charged — the scheduled one where a downgrade is pending,
        // which is the difference between showing a member what they will pay and what they pay now.
        var code = subscription.PendingPlanCode ?? subscription.PlanCode;
        var plan = await _plans.FindByCodeAsync(code, cancellationToken);

        if (plan is null || subscription.CurrentPeriod.EndsAt is not { } chargeAt)
        {
            return Result.Success<IReadOnlyList<UpcomingChargeResponse>>([]);
        }

        var symbol = await _currencies.SymbolAsync(plan.Currency, cancellationToken);
        var willRenew = !subscription.CancelAtPeriodEnd && !plan.IsFree;

        return Result.Success<IReadOnlyList<UpcomingChargeResponse>>(
        [
            new UpcomingChargeResponse
            {
                SubscriptionId = subscription.Id.Value,
                PlanCode = plan.Code,
                Amount = FinancialMapper.ToResponse(
                    willRenew ? plan.Price : Money.Zero(plan.Currency, plan.Exponent), symbol),
                ChargeAt = chargeAt,
                WillRenew = willRenew
            }
        ]);
    }

    private async Task<Result<(Subscription Subscription, SubscriptionPlan From, SubscriptionPlan To)>>
        ResolveAsync(Guid subscriptionId, string planCode, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<(Subscription, SubscriptionPlan, SubscriptionPlan)>(caller.Error);
        }

        var subscription = await _subscriptions.FindAsync(
            SubscriptionId.From(subscriptionId), cancellationToken);

        // 404 rather than 403 on somebody else's subscription: a 403 confirms it exists.
        if (subscription is null || subscription.SubscriberUserId != caller.Value)
        {
            return Result.Failure<(Subscription, SubscriptionPlan, SubscriptionPlan)>(
                Error.NotFound(
                    FinancialErrorCodes.SubscriptionNotFound,
                    "That subscription does not exist."));
        }

        if (!subscription.GrantsAccess)
        {
            return Result.Failure<(Subscription, SubscriptionPlan, SubscriptionPlan)>(
                Error.Conflict(
                    FinancialErrorCodes.SubscriptionChangeNotPermitted,
                    "That subscription has ended. Start a new one."));
        }

        var from = await _plans.FindByCodeAsync(subscription.PlanCode, cancellationToken);
        var to = await _plans.FindByCodeAsync(planCode, cancellationToken);

        if (from is null || to is null || !to.IsActive)
        {
            return Result.Failure<(Subscription, SubscriptionPlan, SubscriptionPlan)>(
                Error.NotFound(FinancialErrorCodes.PlanNotFound, "There is no such plan."));
        }

        if (string.Equals(from.Code, to.Code, StringComparison.Ordinal))
        {
            return Result.Failure<(Subscription, SubscriptionPlan, SubscriptionPlan)>(
                Error.Conflict(
                    FinancialErrorCodes.SubscriptionSamePlan,
                    "That is the plan you already hold."));
        }

        return Result.Success((subscription, from, to));
    }
}
