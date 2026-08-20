using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Delivery;

/// <summary>
/// The delivery record for one message.
/// <para>
/// <b>The words are blanked before the response leaves.</b> An operator investigating "why did this
/// member not get their interview reminder" needs the channels, the attempts and the reason codes.
/// They do not need the sentence naming the employer, and a surface that returned it would make
/// every support session a disclosure.
/// </para>
/// </summary>
public sealed class GetDeliveryHistoryQueryHandler
    : IQueryHandler<GetDeliveryHistoryQuery, NotificationDetailResponse>
{
    private const string Redacted = "[redacted]";

    private readonly INotificationRepository _notifications;
    private readonly NotificationAuthorizationService _authorization;

    public GetDeliveryHistoryQueryHandler(
        INotificationRepository notifications, NotificationAuthorizationService authorization)
    {
        _notifications = notifications;
        _authorization = authorization;
    }

    public async Task<Result<NotificationDetailResponse>> Handle(
        GetDeliveryHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<NotificationDetailResponse>(staff.Error);
        }

        var notification = await _notifications.GetAsync(
            NotificationId.From(query.NotificationId), cancellationToken);

        if (notification is null)
        {
            return Result.Failure<NotificationDetailResponse>(NotificationErrors.NotificationNotFound);
        }

        var detail = NotificationMapper.ToDetail(
            notification,
            new Dictionary<string, Domain.Catalog.NotificationCategory>(StringComparer.Ordinal),
            includeDeliveries: true);

        return Result.Success(detail with
        {
            Notification = detail.Notification with
            {
                Title = Redacted,
                Body = Redacted,
                Actions = []
            }
        });
    }
}
