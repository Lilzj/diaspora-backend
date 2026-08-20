namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Registers or refreshes a device record for the signed-in member.
/// </summary>
public sealed record RegisterDeviceRequest
{
    public required string DeviceIdentifier { get; init; }

    public string? FriendlyName { get; init; }

    /// <summary>One of Android, IOS, Web, Tablet.</summary>
    public required string Platform { get; init; }

    public string? AppVersion { get; init; }

    /// <summary>Push notification registration token. Stored, never used by Identity.</summary>
    public string? PushToken { get; init; }
}
