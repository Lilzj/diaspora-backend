namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// Returned by registration. No tokens are issued — the account holds no usable access
/// until the phone is verified.
/// </summary>
public sealed record RegistrationResponse
{
    public required Guid UserId { get; init; }

    /// <summary>Masked for display, for example "+234******5678".</summary>
    public required string PhoneNumber { get; init; }

    public required string AccessState { get; init; }

    public required OtpChallengeResponse Verification { get; init; }
}
