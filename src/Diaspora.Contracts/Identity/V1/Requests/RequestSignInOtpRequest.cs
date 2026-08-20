namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Starts a passwordless sign-in. The response is identical whether or not the number is
/// registered, so this cannot be used to enumerate accounts.
/// </summary>
public sealed record RequestSignInOtpRequest
{
    public required string PhoneNumber { get; init; }
}
