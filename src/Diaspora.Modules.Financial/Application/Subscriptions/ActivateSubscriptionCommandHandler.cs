using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Subscriptions;

/// <summary>
/// Starts a subscription and opens its first payment.
/// <para>
/// <b>A trial is refused to somebody who has held the plan before.</b> Otherwise the trial is a
/// recurring discount rather than a trial, and it is the single easiest thing on a subscription
/// platform to get wrong.
/// </para>
/// <para>
/// <b>A free plan needs no payment and activates immediately.</b> The free plan is a real plan, so
/// "no subscription" and "the free plan" are the same answer everywhere rather than two.
/// </para>
/// </summary>
public sealed class ActivateSubscriptionCommandHandler
    : ICommandHandler<ActivateSubscriptionCommand, SubscriptionResponse>
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly IPaymentIntentRepository _intents;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateSubscriptionCommandHandler(
        ISubscriptionRepository subscriptions,
        ISubscriptionPlanRepository plans,
        IPaymentIntentRepository intents,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _subscriptions = subscriptions;
        _plans = plans;
        _intents = intents;
        _identities = identities;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubscriptionResponse>> Handle(
        ActivateSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(caller.Error);
        }

        var plan = await _plans.FindByCodeAsync(command.PlanCode, cancellationToken);

        if (plan is null || !plan.IsActive)
        {
            return Result.Failure<SubscriptionResponse>(Error.NotFound(
                FinancialErrorCodes.PlanNotFound, "There is no such plan."));
        }

        var existing = await _subscriptions.FindActiveForSubscriberAsync(
            caller.Value, cancellationToken);

        if (existing is not null && existing.GrantsAccess)
        {
            return Result.Failure<SubscriptionResponse>(Error.Conflict(
                FinancialErrorCodes.SubscriptionAlreadyActive,
                "You already hold a subscription. Cancel it before starting another."));
        }

        var hadBefore = await _subscriptions.HasEverSubscribedAsync(
            caller.Value, plan.Code, cancellationToken);

        var withTrial = command.WithTrial && plan.TrialDays > 0 && !hadBefore;
        var now = _clock.UtcNow;

        var account = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            plan.Currency,
            countryCode: command.CountryCode,
            cancellationToken: cancellationToken);

        var subscription = Subscription.Start(
            SubscriptionId.From(_guids.NewSequentialGuid()),
            caller.Value,
            account.Id,
            plan,
            now,
            () => _guids.NewSequentialGuid(),
            withTrial);

        _subscriptions.Add(subscription);

        // A trial charges nothing now; the renewal sweep opens the first real payment when it ends.
        if (!plan.IsFree && !withTrial)
        {
            var methodKind = Enum.TryParse<PaymentMethodKind>(command.MethodKind, true, out var parsed)
                ? parsed
                : PaymentMethodKind.Card;

            var intent = PaymentIntent.Create(
                PaymentIntentId.From(_guids.NewSequentialGuid()),
                caller.Value,
                account.Id,
                plan.Price,
                methodKind,
                $"{plan.Name} subscription",
                now,
                SubjectReference.ForMember(caller.Value),
                command.IdempotencyKey ?? $"subscription:{subscription.Id.Value:N}",
                command.CountryCode);

            _intents.Add(intent);
            subscription.AttachPayment(intent.Id);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(subscription));
    }
}
