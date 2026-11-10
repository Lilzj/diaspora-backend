using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Devices;

/// <summary>Registers or refreshes a push endpoint for the caller.</summary>
[RequirePermission(NotificationPermissions.DevicesManage)]
[Transactional(Reason = "Creates or refreshes an endpoint and publishes the fact together.")]
[Auditable]
public sealed record RegisterNotificationDeviceCommand : ICommand<DeviceRegistrationResponse>
{
    public required RegisterNotificationDeviceRequest Request { get; init; }
}
