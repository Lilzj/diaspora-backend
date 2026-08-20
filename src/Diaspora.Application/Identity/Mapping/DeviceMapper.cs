using Diaspora.Application.Identity.Validation;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Devices;

namespace Diaspora.Application.Identity.Mapping;

/// <summary>
/// Projects devices for display. The push token is reported as present or absent and
/// never echoed — it is a credential for someone else's service.
/// </summary>
public static class DeviceMapper
{
    public static DeviceResponse ToResponse(UserDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new DeviceResponse
        {
            Id = device.Id.Value,
            DeviceIdentifier = device.DeviceIdentifier,
            FriendlyName = device.FriendlyName,
            Platform = IdentityValueParser.Format(device.Platform),
            AppVersion = device.AppVersion,
            HasPushToken = !string.IsNullOrEmpty(device.PushToken),
            IsTrusted = device.IsTrusted,
            RegisteredAt = device.CreatedAt,
            LastActiveAt = device.LastActiveAt
        };
    }

    public static IReadOnlyList<DeviceResponse> ToResponses(IEnumerable<UserDevice> devices) =>
        devices.Select(ToResponse).ToArray();
}
