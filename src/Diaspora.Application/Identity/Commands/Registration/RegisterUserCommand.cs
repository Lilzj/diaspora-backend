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
public sealed record RegisterUserCommand : ICommand<RegistrationResponse>
{
    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    public required string Password { get; init; }

    public required string Nationality { get; init; }

    public required string CountryOfResidence { get; init; }

    public string? PreferredLanguage { get; init; }

    public string? PreferredTimeZone { get; init; }
}
