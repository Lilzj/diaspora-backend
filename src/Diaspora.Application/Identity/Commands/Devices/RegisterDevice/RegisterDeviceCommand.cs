using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Devices.RegisterDevice;

/// <summary>
/// Registers or refreshes a device for the signed-in member. Idempotent on
/// <c>DeviceIdentifier</c>, so a client can call it on every launch.
/// </summary>
[Auditable(Action = "identity.device.registered")]
[Authorize(Permission = "identity:devices:manage")]
public sealed record RegisterDeviceCommand : ICommand<DeviceResponse>
{
    public required string DeviceIdentifier { get; init; }

    public string? FriendlyName { get; init; }

    public required string Platform { get; init; }

    public string? AppVersion { get; init; }

    public string? PushToken { get; init; }
}
