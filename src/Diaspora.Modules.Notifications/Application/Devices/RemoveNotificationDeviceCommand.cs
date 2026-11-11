using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Notifications.Application.Devices;

/// <summary>The member removed a device. Nothing further is delivered to it.</summary>
[RequirePermission(NotificationPermissions.DevicesManage)]
[Transactional(Reason = "Retires the endpoint and records the fact together.")]
[Auditable]
public sealed record RemoveNotificationDeviceCommand : ICommand
{
    public required Guid DeviceId { get; init; }
}
