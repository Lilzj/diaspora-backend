namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Redeems the phone verification code. On success the member is promoted to limited
/// access and receives their first token pair, so no separate sign-in is needed.
/// </summary>
public sealed record VerifyPhoneRequest
{
    public required string PhoneNumber { get; init; }

    public required string Code { get; init; }

    public string? DeviceIdentifier { get; init; }

    public string? DeviceName { get; init; }

    public string? Platform { get; init; }

    public string? AppVersion { get; init; }
}
