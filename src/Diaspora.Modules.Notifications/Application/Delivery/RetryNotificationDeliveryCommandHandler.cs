using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Delivery;

/// <summary>Requeues a failed delivery. Staff only.</summary>
public sealed class RetryNotificationDeliveryCommandHandler
    : ICommandHandler<RetryNotificationDeliveryCommand, NotificationDeliveryResponse>
{
    private readonly INotificationRepository _notifications;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RetryNotificationDeliveryCommandHandler(
        INotificationRepository notifications,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _notifications = notifications;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<NotificationDeliveryResponse>> Handle(
        RetryNotificationDeliveryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<NotificationDeliveryResponse>(staff.Error);
        }

        if (NotificationMapper.ParseEnum<NotificationChannelKind>(command.Channel)
            is not { } channel)
        {
            return Result.Failure<NotificationDeliveryResponse>(NotificationErrors.ChannelUnavailable);
        }

        var notification = await _notifications.GetAsync(
            NotificationId.From(command.NotificationId), cancellationToken);

        if (notification is null)
        {
            return Result.Failure<NotificationDeliveryResponse>(
                NotificationErrors.NotificationNotFound);
        }

        var requeued = notification.Requeue(channel, _clock.UtcNow);

        if (requeued.IsFailure)
        {
            return Result.Failure<NotificationDeliveryResponse>(requeued.Error);
        }

        _notifications.Update(notification);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var delivery = notification.DeliveryFor(channel)!;

        return Result.Success(NotificationMapper.ToResponse(delivery));
    }
}
