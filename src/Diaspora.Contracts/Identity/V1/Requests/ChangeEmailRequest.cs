namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Asks to move the sign-in address to <see cref="Address"/>. Nothing changes until the link sent
/// there is confirmed.
/// </summary>
public sealed record ChangeEmailRequest
{
    /// <summary>The new address.</summary>
    public string? Address { get; init; }

    /// <summary>
    /// The member's password. Required: a session alone must not be enough to take an account,
    /// and moving the sign-in address is how an account is taken.
    /// </summary>
    public string? CurrentPassword { get; init; }
}

/// <summary>The token from an email change link, posted by the app when it opened the link.</summary>
public sealed record ConfirmEmailChangeRequest
{
    public string? Token { get; init; }
}
