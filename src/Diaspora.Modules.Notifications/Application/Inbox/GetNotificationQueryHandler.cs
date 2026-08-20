using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>One of the caller's own messages.</summary>
public sealed class GetNotificationQueryHandler
    : IQueryHandler<GetNotificationQuery, NotificationDetailResponse>
{
    private readonly INotificationRepository _notifications;
    private readonly INotificationCategoryRepository _categories;
    private readonly NotificationAuthorizationService _authorization;

    public GetNotificationQueryHandler(
        INotificationRepository notifications,
        INotificationCategoryRepository categories,
        NotificationAuthorizationService authorization)
    {
        _notifications = notifications;
        _categories = categories;
        _authorization = authorization;
    }

    public async Task<Result<NotificationDetailResponse>> Handle(
        GetNotificationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NotificationDetailResponse>(caller.Error);
        }

        var loaded = await _notifications.GetAsync(
            NotificationId.From(query.NotificationId), cancellationToken);

        var owned = NotificationAuthorizationService.RequireOwnership(loaded, caller.Value);

        if (owned.IsFailure)
        {
            return Result.Failure<NotificationDetailResponse>(owned.Error);
        }

        var categories = await _categories.ListActiveAsync(cancellationToken);

        return Result.Success(NotificationMapper.ToDetail(
            owned.Value,
            categories.ToDictionary(category => category.Key, StringComparer.Ordinal),
            includeDeliveries: true));
    }
}
