using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Devices;

namespace Diaspora.Modules.Notifications.Application.Mapping;

/// <summary>
/// Turns endpoints into wire responses.
/// <para>
/// The token is reported as a boolean and never as a value. It is a credential for somebody else's
/// service, and a response carrying it would put it in every client's log and every proxy's cache.
/// </para>
/// </summary>
public static class DeviceMapper
{
    /// <param name="sessionDeviceId">
    /// The device the caller's token names, or null when it names none — B13.3. Compared against
    /// what was recorded at registration to decide <c>isCurrentDevice</c>; two unknowns are not a
    /// match, or every endpoint a member ever registered would claim to be the phone in their hand.
    /// </param>
    public static DeviceRegistrationResponse ToResponse(
        DeviceRegistration device, Guid? sessionDeviceId = null)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new DeviceRegistrationResponse
        {
            IsCurrentDevice = device.IsCurrentFor(sessionDeviceId),
            Id = device.Id.Value,
            Platform = device.Platform.ToString(),
            Channel = device.Channel.ToString(),
            FriendlyName = device.FriendlyName,
            DeviceIdentifier = device.DeviceIdentifier,
            AppVersion = device.AppVersion,
            HasToken = !string.IsNullOrEmpty(device.Token),
            IsActive = device.IsDeliverable,
            RegisteredAt = device.RegisteredAt,
            LastActiveAt = device.LastActiveAt,
            LastDeliveredAt = device.LastDeliveredAt,
            SuppressionReason = device.SuppressionReason
        };
    }
}
