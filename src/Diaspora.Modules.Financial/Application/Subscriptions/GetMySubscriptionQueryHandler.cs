using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Subscriptions;

/// <summary>Reads the caller's subscription.</summary>
public sealed class GetMySubscriptionQueryHandler
    : IQueryHandler<GetMySubscriptionQuery, SubscriptionResponse?>
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly FinancialAuthorizationService _authorization;

    public GetMySubscriptionQueryHandler(
        ISubscriptionRepository subscriptions, FinancialAuthorizationService authorization)
    {
        _subscriptions = subscriptions;
        _authorization = authorization;
    }

    public async Task<Result<SubscriptionResponse?>> Handle(
        GetMySubscriptionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<SubscriptionResponse?>(caller.Error);
        }

        var subscription = await _subscriptions.FindActiveForSubscriberAsync(
            caller.Value, cancellationToken);

        return Result.Success(
            subscription is null ? null : FinancialMapper.ToResponse(subscription));
    }
}
