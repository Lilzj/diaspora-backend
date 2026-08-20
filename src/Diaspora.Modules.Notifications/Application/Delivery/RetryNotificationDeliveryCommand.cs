using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Delivery;

/// <summary>
/// An operator asked for one more attempt on a failed channel.
/// <para>
/// Only a genuinely failed delivery can be retried. A suppressed one cannot — suppression was the
/// member's own decision, and an operations console that could override a stated preference is
/// precisely the console nobody should build.
/// </para>
/// </summary>
[Authorize(Permission = NotificationPermissions.DeliveryRetry)]
[Transactional(Reason = "Requeues the delivery and records the decision together.")]
[Auditable]
public sealed record RetryNotificationDeliveryCommand : ICommand<NotificationDeliveryResponse>
{
    public required Guid NotificationId { get; init; }

    /// <summary><c>InApp</c>, <c>Push</c>, <c>Email</c>.</summary>
    public required string Channel { get; init; }
}
