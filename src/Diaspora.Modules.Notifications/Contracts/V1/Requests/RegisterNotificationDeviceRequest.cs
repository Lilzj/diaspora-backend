namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>
/// Registers or refreshes a push endpoint.
/// <para>
/// Property names deliberately match Identity's <c>RegisterDeviceRequest</c> — <c>deviceIdentifier</c>,
/// <c>platform</c>, <c>appVersion</c> — so a client that already builds that payload for sign-in
/// sends the same object here. The two records are different things (see <c>DeviceRegistration</c>)
/// but a client should not have to know that to fill in a form.
/// </para>
/// </summary>
public sealed record RegisterNotificationDeviceRequest
{
    /// <summary>The provider token. Stored, never returned, never logged.</summary>
    public required string Token { get; init; }

    /// <summary>One of Android, IOS, Web, Tablet.</summary>
    public required string Platform { get; init; }

    /// <summary>The client's own stable device string, so Identity's record can be correlated.</summary>
    public string? DeviceIdentifier { get; init; }

    public string? FriendlyName { get; init; }

    public string? AppVersion { get; init; }
}
