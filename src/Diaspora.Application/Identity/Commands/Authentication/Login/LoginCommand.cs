using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Authentication.Login;

/// <summary>
/// Exchanges a phone number and password for a session and token pair.
/// </summary>
[Auditable(Action = "identity.user.logged-in")]
public sealed record LoginCommand : ICommand<AuthenticationResponse>
{
    /// <summary>The address the account was registered with. Was <c>PhoneNumber</c>.</summary>
    public required string Email { get; init; }

    public required string Password { get; init; }

    public string? DeviceIdentifier { get; init; }

    public string? DeviceName { get; init; }

    public string? Platform { get; init; }

    public string? AppVersion { get; init; }
}
