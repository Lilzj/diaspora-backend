using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member read a message.
/// <para>
/// The counter is not touched here. The aggregate raises an opened event only on a first read, and
/// its handler settles the badge — so a list screen marking twenty visible messages read, or a client
/// retrying on a flaky connection, cannot decrement anything twice.
/// </para>
/// </summary>
public sealed class MarkNotificationReadCommandHandler
    : ICommandHandler<MarkNotificationReadCommand, NotificationResponse>
{
    private readonly NotificationMutationService _mutations;
    private readonly IClock _clock;

    public MarkNotificationReadCommandHandler(NotificationMutationService mutations, IClock clock)
    {
        _mutations = mutations;
        _clock = clock;
    }

    public Task<Result<NotificationResponse>> Handle(
        MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var channel = NotificationMapper.ParseEnum<NotificationChannelKind>(command.Channel)
                      ?? NotificationChannelKind.InApp;

        return _mutations.ApplyAsync(
            command.NotificationId,
            (notification, _) => notification.MarkRead(channel.ToString(), _clock.UtcNow),
            cancellationToken);
    }
}
