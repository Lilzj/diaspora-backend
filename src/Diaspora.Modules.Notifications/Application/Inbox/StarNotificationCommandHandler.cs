using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>Stars or unstars a message.</summary>
public sealed class StarNotificationCommandHandler
    : ICommandHandler<StarNotificationCommand, NotificationResponse>
{
    private readonly NotificationMutationService _mutations;
    private readonly InboxCounterService _counters;

    public StarNotificationCommandHandler(
        NotificationMutationService mutations, InboxCounterService counters)
    {
        _mutations = mutations;
        _counters = counters;
    }

    public Task<Result<NotificationResponse>> Handle(
        StarNotificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _mutations.ApplyAsync(
            command.NotificationId,
            async (notification, userId, token) =>
            {
                if (notification.IsStarred == command.IsStarred)
                {
                    return Result.Success();
                }

                if (!command.IsStarred)
                {
                    notification.Unstar();
                    await _counters.RecordStarredAsync(userId, -1, token);

                    return Result.Success();
                }

                var result = notification.Star();

                if (result.IsSuccess)
                {
                    await _counters.RecordStarredAsync(userId, 1, token);
                }

                return result;
            },
            cancellationToken);
    }
}
