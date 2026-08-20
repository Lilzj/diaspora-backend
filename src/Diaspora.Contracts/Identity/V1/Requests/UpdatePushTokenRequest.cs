namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Replaces a device's push registration token. Send null to clear it.
/// </summary>
public sealed record UpdatePushTokenRequest
{
    public string? PushToken { get; init; }
}
