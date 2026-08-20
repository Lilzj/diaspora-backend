using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.Devices;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Services;

/// <inheritdoc cref="IDeviceRegistrationService" />
public sealed class DeviceRegistrationService : IDeviceRegistrationService
{
    private readonly IUserDeviceRepository _devices;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IClock _clock;
    private readonly ILogger<DeviceRegistrationService> _logger;

    public DeviceRegistrationService(
        IUserDeviceRepository devices,
        IIdentityPolicyProvider policies,
        IClock clock,
        ILogger<DeviceRegistrationService> logger)
    {
        _devices = devices;
        _policies = policies;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DeviceId?> ResolveAsync(
        User user,
        string? deviceIdentifier,
        string? deviceName,
        DevicePlatform platform,
        string? appVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(deviceIdentifier))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var identifier = deviceIdentifier.Trim();

        var existing = await _devices.GetByIdentifierAsync(user.Id, identifier, cancellationToken);
        if (existing is not null)
        {
            existing.RecordActivity(now, appVersion);
            _devices.Update(existing);
            return existing.Id;
        }

        var registered = await _devices.CountByUserAsync(user.Id, cancellationToken);
        if (registered >= _policies.Current.MaxDevicesPerUser)
        {
            // Refusing the sign-in over a device-list cap would be a poor trade. The
            // session proceeds without a device record; the member can prune the list
            // from their account settings.
            _logger.LogWarning(
                "User {UserId} has reached the device limit ({Limit}); continuing without a device record.",
                user.Id, _policies.Current.MaxDevicesPerUser);
            return null;
        }

        var device = UserDevice.Register(user.Id, identifier, deviceName ?? string.Empty, platform, appVersion, now);
        await _devices.AddAsync(device, cancellationToken);

        return device.Id;
    }
}
