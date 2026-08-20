using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member brought a filed message back.
/// <para>
/// The counters are settled here rather than from an event, because un-archiving raises none: the
/// archived event already happened and is part of the record. What changes is where the message
/// lives, and the counter has to follow it back.
/// </para>
/// </summary>
public sealed class RestoreNotificationCommandHandler
    : ICommandHandler<RestoreNotificationCommand, NotificationResponse>
{
    private readonly NotificationMutationService _mutations;
    private readonly InboxCounterService _counters;

    public RestoreNotificationCommandHandler(
        NotificationMutationService mutations, InboxCounterService counters)
    {
        _mutations = mutations;
        _counters = counters;
    }

    public Task<Result<NotificationResponse>> Handle(
        RestoreNotificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _mutations.ApplyAsync(
            command.NotificationId,
            async (notification, userId, token) =>
            {
                var result = notification.Unarchive();

                if (result.IsSuccess)
                {
                    await _counters.RecordUnarchivedAsync(
                        userId, notification.CategoryKey, notification.IsUnread, token);
                }

                return result;
            },
            cancellationToken);
    }
}
