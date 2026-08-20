namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Completes a passwordless sign-in by redeeming the code sent to the member's phone.
/// On success a session opens and a token pair is issued — the same response every sign-in
/// method produces.
/// </summary>
public sealed record OtpSignInRequest
{
    public required string PhoneNumber { get; init; }

    public required string Code { get; init; }

    public string? DeviceIdentifier { get; init; }

    public string? DeviceName { get; init; }

    public string? Platform { get; init; }

    public string? AppVersion { get; init; }
}
