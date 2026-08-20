using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// Pins or unpins a message.
/// <para>
/// The ceiling is enforced here rather than in the aggregate, because a notification cannot count
/// its siblings — and a pinned inbox is an unpinned inbox, so the limit is real rather than
/// defensive.
/// </para>
/// </summary>
public sealed class PinNotificationCommandHandler
    : ICommandHandler<PinNotificationCommand, NotificationResponse>
{
    private readonly NotificationMutationService _mutations;
    private readonly INotificationRepository _notifications;
    private readonly InboxCounterService _counters;

    public PinNotificationCommandHandler(
        NotificationMutationService mutations,
        INotificationRepository notifications,
        InboxCounterService counters)
    {
        _mutations = mutations;
        _notifications = notifications;
        _counters = counters;
    }

    public Task<Result<NotificationResponse>> Handle(
        PinNotificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _mutations.ApplyAsync(
            command.NotificationId,
            async (notification, userId, token) =>
            {
                if (!command.IsPinned)
                {
                    if (!notification.IsPinned)
                    {
                        return Result.Success();
                    }

                    notification.Unpin();
                    await _counters.RecordPinnedAsync(userId, -1, token);

                    return Result.Success();
                }

                if (notification.IsPinned)
                {
                    return Result.Success();
                }

                var pinned = await _notifications.CountPinnedAsync(userId, token);

                if (pinned >= NotificationLimits.MaxPinnedNotifications)
                {
                    return Result.Failure(NotificationErrors.PinLimitReached);
                }

                var result = notification.Pin();

                if (result.IsSuccess)
                {
                    await _counters.RecordPinnedAsync(userId, 1, token);
                }

                return result;
            },
            cancellationToken);
    }
}
