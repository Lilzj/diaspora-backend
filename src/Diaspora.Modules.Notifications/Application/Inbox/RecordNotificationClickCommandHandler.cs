using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member followed a notification somewhere.
/// <para>
/// The aggregate backfills the open when a click arrives without one — which is the normal case for
/// a push banner tapped from a lock screen. Without it every open rate would under-count by exactly
/// the members who acted fastest.
/// </para>
/// </summary>
public sealed class RecordNotificationClickCommandHandler
    : ICommandHandler<RecordNotificationClickCommand, NotificationResponse>
{
    private readonly NotificationMutationService _mutations;
    private readonly IClock _clock;

    public RecordNotificationClickCommandHandler(
        NotificationMutationService mutations, IClock clock)
    {
        _mutations = mutations;
        _clock = clock;
    }

    public Task<Result<NotificationResponse>> Handle(
        RecordNotificationClickCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var channel = NotificationMapper.ParseEnum<NotificationChannelKind>(command.Channel)
                      ?? NotificationChannelKind.InApp;

        return _mutations.ApplyAsync(
            command.NotificationId,
            (notification, _) => notification.RecordClick(
                command.ActionKey, channel.ToString(), _clock.UtcNow),
            cancellationToken);
    }
}
