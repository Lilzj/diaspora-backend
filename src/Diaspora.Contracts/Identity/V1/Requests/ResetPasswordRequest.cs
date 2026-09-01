namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Completes account recovery by redeeming the recovery code and setting a new password.
/// Every session on the account is revoked as a side effect.
/// </summary>
public sealed record ResetPasswordRequest
{
    /// <summary>The address the account was registered with.</summary>
    public required string Email { get; init; }

    /// <summary>The one-time recovery code delivered to the member's phone.</summary>
    public required string Code { get; init; }

    public required string NewPassword { get; init; }
}
