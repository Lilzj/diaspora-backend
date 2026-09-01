namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Credential exchange. Device details are optional but strongly encouraged — they are
/// what makes the member's session list intelligible.
/// </summary>
public sealed record LoginRequest
{
    /// <summary>
    /// The address the account was registered with.
    /// </summary>
    /// <remarks>
    /// This was <c>PhoneNumber</c>. Sign-in moved to an email address and a password so
    /// that signing in costs nothing to deliver: the one-time code path charged for an
    /// SMS on every sign-in, and a password does not. SMS is now spent only where it
    /// earns its cost — proving the member holds the phone number they claim.
    /// </remarks>
    public required string Email { get; init; }

    public required string Password { get; init; }

    /// <summary>Stable client-generated device identifier, so repeat sign-ins reuse one device record.</summary>
    public string? DeviceIdentifier { get; init; }

    /// <summary>Human-readable device name shown in the session list, for example "Amara's Pixel 8".</summary>
    public string? DeviceName { get; init; }

    /// <summary>One of Android, IOS, Web, Tablet. Unrecognised values are treated as Unknown.</summary>
    public string? Platform { get; init; }

    public string? AppVersion { get; init; }
}
