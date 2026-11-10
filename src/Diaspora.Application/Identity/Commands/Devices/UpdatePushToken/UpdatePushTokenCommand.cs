using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Devices.UpdatePushToken;

/// <summary>
/// Replaces a device's push registration token. Send null to clear it.
/// </summary>
[RequirePermission("identity:devices:manage")]
public sealed record UpdatePushTokenCommand : ICommand<DeviceResponse>
{
    public required Guid DeviceId { get; init; }

    public string? PushToken { get; init; }
}
