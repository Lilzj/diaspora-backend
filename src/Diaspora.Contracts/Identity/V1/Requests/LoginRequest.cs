namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Credential exchange. Device details are optional but strongly encouraged — they are
/// what makes the member's session list intelligible.
/// </summary>
public sealed record LoginRequest
{
    public required string PhoneNumber { get; init; }

    public required string Password { get; init; }

    /// <summary>Stable client-generated device identifier, so repeat sign-ins reuse one device record.</summary>
    public string? DeviceIdentifier { get; init; }

    /// <summary>Human-readable device name shown in the session list, for example "Amara's Pixel 8".</summary>
    public string? DeviceName { get; init; }

    /// <summary>One of Android, IOS, Web, Tablet. Unrecognised values are treated as Unknown.</summary>
    public string? Platform { get; init; }

    public string? AppVersion { get; init; }
}
