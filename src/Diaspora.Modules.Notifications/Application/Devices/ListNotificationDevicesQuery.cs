using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Devices;

/// <summary>Everything the caller can be reached on, including endpoints that have stopped working.</summary>
[Authorize(Permission = NotificationPermissions.DevicesManage)]
public sealed record ListNotificationDevicesQuery : IQuery<IReadOnlyList<DeviceRegistrationResponse>>;
