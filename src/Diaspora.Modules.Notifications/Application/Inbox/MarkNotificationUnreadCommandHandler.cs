using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member put a message back to unread.
/// <para>
/// The badge is settled directly rather than through a domain event, because there is no "unread"
/// event to raise: the open already happened and the analytics keep it. What the member is undoing
/// is the counter, and undoing it explicitly is the honest expression of that.
/// </para>
/// </summary>
public sealed class MarkNotificationUnreadCommandHandler
    : ICommandHandler<MarkNotificationUnreadCommand, NotificationResponse>
{
    private readonly NotificationMutationService _mutations;
    private readonly InboxCounterService _counters;

    public MarkNotificationUnreadCommandHandler(
        NotificationMutationService mutations, InboxCounterService counters)
    {
        _mutations = mutations;
        _counters = counters;
    }

    public Task<Result<NotificationResponse>> Handle(
        MarkNotificationUnreadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _mutations.ApplyAsync(
            command.NotificationId,
            async (notification, userId, token) =>
            {
                var wasRead = notification.Engagement.IsOpened;
                var result = notification.MarkUnread();

                if (result.IsSuccess && wasRead)
                {
                    await _counters.RecordUnreadAsync(userId, notification.CategoryKey, token);
                }

                return result;
            },
            cancellationToken);
    }
}
