using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Events;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Domain.Identity.Devices;

/// <summary>
/// A physical device a member signs in from. Kept as its own aggregate because its
/// lifecycle (push-token refresh, rename, last-active heartbeat) is far busier than
/// the account's and must not force the <c>User</c> aggregate to be loaded.
/// </summary>
public sealed class UserDevice : AggregateRoot<DeviceId>, IAuditableEntity
{
    public const int MaxIdentifierLength = 200;
    public const int MaxFriendlyNameLength = 120;
    public const int MaxPushTokenLength = 512;

    public UserId UserId { get; private set; } = default!;

    /// <summary>Stable client-generated identifier, unique per account.</summary>
    public string DeviceIdentifier { get; private set; } = default!;

    public string FriendlyName { get; private set; } = default!;
    public DevicePlatform Platform { get; private set; }
    public string? AppVersion { get; private set; }

    /// <summary>
    /// Placeholder for the push notification registration. Identity stores it; the
    /// notification subsystem owns delivery.
    /// </summary>
    public string? PushToken { get; private set; }

    public DateTimeOffset LastActiveAt { get; private set; }
    public bool IsTrusted { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private UserDevice() { }

    private UserDevice(
        DeviceId id,
        UserId userId,
        string deviceIdentifier,
        string friendlyName,
        DevicePlatform platform,
        string? appVersion,
        DateTimeOffset now) : base(id)
    {
        UserId = userId;
        DeviceIdentifier = deviceIdentifier;
        FriendlyName = friendlyName;
        Platform = platform;
        AppVersion = appVersion;
        LastActiveAt = now;
        IsTrusted = false;
    }

    public static UserDevice Register(
        UserId userId,
        string deviceIdentifier,
        string friendlyName,
        DevicePlatform platform,
        string? appVersion,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(userId);
        Guard.Against.NullOrWhiteSpace(deviceIdentifier, nameof(deviceIdentifier));
        Guard.Against.MaxLength(deviceIdentifier, MaxIdentifierLength, nameof(deviceIdentifier));

        var name = string.IsNullOrWhiteSpace(friendlyName)
            ? DefaultNameFor(platform)
            : friendlyName.Trim();
        Guard.Against.MaxLength(name, MaxFriendlyNameLength, nameof(friendlyName));

        var device = new UserDevice(
            DeviceId.New(), userId, deviceIdentifier.Trim(), name, platform, Truncate(appVersion, 40), now);

        device.RaiseDomainEvent(new UserDeviceRegisteredDomainEvent(
            userId, device.Id, device.DeviceIdentifier, platform, now));

        return device;
    }

    public Result Rename(string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
            return Result.Failure(Error.Validation(
                "Identity.Device.NameRequired", "A device name is required."));

        var name = friendlyName.Trim();
        if (name.Length > MaxFriendlyNameLength)
            return Result.Failure(Error.Validation(
                "Identity.Device.NameTooLong",
                $"A device name cannot exceed {MaxFriendlyNameLength} characters."));

        FriendlyName = name;
        return Result.Success();
    }

    public Result UpdatePushToken(string? pushToken)
    {
        if (pushToken is not null && pushToken.Length > MaxPushTokenLength)
            return Result.Failure(Error.Validation(
                "Identity.Device.PushTokenTooLong",
                $"A push token cannot exceed {MaxPushTokenLength} characters."));

        PushToken = string.IsNullOrWhiteSpace(pushToken) ? null : pushToken.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Refreshes the heartbeat and, when supplied, the reported app version.
    /// </summary>
    public void RecordActivity(DateTimeOffset now, string? appVersion = null)
    {
        LastActiveAt = now;
        if (!string.IsNullOrWhiteSpace(appVersion))
        {
            AppVersion = Truncate(appVersion, 40);
        }
    }

    /// <summary>
    /// Marks the device as trusted. Reserved for a future step-up flow — nothing in
    /// B2 grants trust automatically.
    /// </summary>
    public void MarkTrusted() => IsTrusted = true;

    public void RevokeTrust() => IsTrusted = false;

    private static string DefaultNameFor(DevicePlatform platform) => platform switch
    {
        DevicePlatform.Android => "Android device",
        DevicePlatform.IOS => "iOS device",
        DevicePlatform.Web => "Web browser",
        DevicePlatform.Tablet => "Tablet",
        _ => "Unknown device"
    };

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
