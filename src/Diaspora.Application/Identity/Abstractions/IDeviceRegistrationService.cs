using Diaspora.Domain.Identity.Devices;
using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Resolves the device a request arrived from, creating a record the first time one is
/// seen and refreshing its heartbeat afterwards.
/// <para>
/// Shared by sign-in and phone verification so a member's device list is identical
/// whichever door they came through.
/// </para>
/// </summary>
public interface IDeviceRegistrationService
{
    /// <summary>
    /// Returns the device id for the supplied identifier, registering it if new. Returns
    /// null when no identifier was supplied — a client that does not report a device still
    /// gets a session, just an anonymous one.
    /// </summary>
    Task<DeviceId?> ResolveAsync(
        User user,
        string? deviceIdentifier,
        string? deviceName,
        DevicePlatform platform,
        string? appVersion,
        CancellationToken cancellationToken = default);
}
