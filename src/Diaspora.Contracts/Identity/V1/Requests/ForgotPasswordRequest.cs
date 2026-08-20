namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Starts account recovery. The response is identical whether or not the number is
/// registered, so this endpoint cannot be used to enumerate accounts.
/// </summary>
public sealed record ForgotPasswordRequest
{
    public required string PhoneNumber { get; init; }
}
