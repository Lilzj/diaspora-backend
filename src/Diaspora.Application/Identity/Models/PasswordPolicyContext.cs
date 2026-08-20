namespace Diaspora.Application.Identity.Models;

/// <summary>
/// Member-specific context for password validation, so a password cannot simply repeat
/// the member's own phone number or name.
/// </summary>
public sealed record PasswordPolicyContext
{
    public string? PhoneNumber { get; init; }

    public string? FullName { get; init; }

    public string? Email { get; init; }

    public static readonly PasswordPolicyContext Empty = new();
}
