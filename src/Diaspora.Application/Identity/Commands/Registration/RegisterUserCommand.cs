using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Registration;

/// <summary>
/// Creates a Diaspora account.
/// <para>
/// Deliberately issues no tokens. Registration produces an account that can do nothing
/// until the phone number is confirmed, which is what makes phone verification a real
/// gate rather than a dismissible prompt.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.registered")]
[AllowAnonymous(
    "Authentication itself. A caller who could already prove who they are would have no reason to " +
    "be here.")]
public sealed record RegisterUserCommand : ICommand<RegistrationResponse>
{
    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    /// <summary>The address the member will sign in with. Unique across the platform.</summary>
    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string Nationality { get; init; }

    public required string CountryOfResidence { get; init; }

    public string? PreferredLanguage { get; init; }

    public string? PreferredTimeZone { get; init; }

    /// <summary>The inviting member's referral code, when there is one. Announced, never stored.</summary>
    public string? ReferralCode { get; init; }
}
