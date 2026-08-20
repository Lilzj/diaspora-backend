using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Authentication.Login;

/// <summary>
/// Exchanges a phone number and password for a session and token pair.
/// </summary>
[Auditable(Action = "identity.user.logged-in")]
public sealed record LoginCommand : ICommand<AuthenticationResponse>
{
    public required string PhoneNumber { get; init; }

    public required string Password { get; init; }

    public string? DeviceIdentifier { get; init; }

    public string? DeviceName { get; init; }

    public string? Platform { get; init; }

    public string? AppVersion { get; init; }
}
