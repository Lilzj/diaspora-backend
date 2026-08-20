namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// One notification with its delivery record.
/// <para>
/// The member sees the notification and their own deliveries; staff reading somebody else's see the
/// deliveries and never the notification. The split is enforced by two different endpoints rather
/// than by a nullable field, so a mistake in one cannot expose the other.
/// </para>
/// </summary>
public sealed record NotificationDetailResponse
{
    public required NotificationResponse Notification { get; init; }

    public IReadOnlyList<NotificationDeliveryResponse> Deliveries { get; init; } = [];

    public IReadOnlyList<NotificationDeliveryAttemptResponse> Attempts { get; init; } = [];
}
