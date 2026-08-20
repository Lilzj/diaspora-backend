using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Subscriptions;

/// <summary>Cancels a subscription.</summary>
public sealed class CancelSubscriptionCommandHandler
    : ICommandHandler<CancelSubscriptionCommand, SubscriptionResponse>
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSubscriptionCommandHandler(
        ISubscriptionRepository subscriptions,
        FinancialAuthorizationService authorization,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _subscriptions = subscriptions;
        _authorization = authorization;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubscriptionResponse>> Handle(
        CancelSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(caller.Error);
        }

        var subscription = await _subscriptions.FindAsync(
            SubscriptionId.From(command.SubscriptionId), cancellationToken);

        var isStaff = _authorization.Has(FinancialPermissions.WalletManage);

        if (subscription is null || (subscription.SubscriberUserId != caller.Value && !isStaff))
        {
            return Result.Failure<SubscriptionResponse>(Error.NotFound(
                FinancialErrorCodes.SubscriptionNotFound, "That subscription does not exist."));
        }

        try
        {
            // Only staff may end access before the paid period runs out.
            if (command.Immediately && isStaff)
            {
                subscription.End(SubscriptionStatus.Cancelled, command.Reason, _clock.UtcNow);
            }
            else
            {
                subscription.CancelAtEndOfPeriod(command.Reason, _clock.UtcNow);
            }
        }
        catch (DomainException exception)
        {
            return Result.Failure<SubscriptionResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        _subscriptions.Update(subscription);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(subscription));
    }
}
