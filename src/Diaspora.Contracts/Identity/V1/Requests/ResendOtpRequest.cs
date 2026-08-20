namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Requests a fresh one-time code. Subject to both a cooldown and a total resend budget.
/// </summary>
public sealed record ResendOtpRequest
{
    public required string PhoneNumber { get; init; }

    /// <summary>One of PhoneVerification, PasswordReset, AccountRecovery. Defaults to PhoneVerification.</summary>
    public string? Purpose { get; init; }
}
