using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Authentication.Authenticate;

/// <summary>
/// Authenticates through a chosen method and issues a session and token pair.
/// <para>
/// The method-agnostic sign-in. It resolves the provider named by <see cref="Method"/> from
/// the registry, hands it the credentials, and — on success — runs the same access-state
/// gate and token issuance every sign-in shares. Adding Google or a magic link adds a
/// provider and a method key; this command does not change.
/// </para>
/// <para>
/// The password provider is reachable here too, but the dedicated <c>/auth/login</c>
/// endpoint remains for backward compatibility and is unchanged. This backs the frontend's
/// passwordless path, <c>POST /auth/otp/verify</c>.
/// </para>
/// </summary>
public sealed record AuthenticateCommand : ICommand<AuthenticationResponse>
{
    /// <summary>The method key — "password" or "phone-otp".</summary>
    public required string Method { get; init; }

    public required string PhoneNumber { get; init; }

    /// <summary>Used by the password method; the one-time code method uses the phone number.</summary>
    public string? Email { get; init; }

    public string? Password { get; init; }

    public string? Code { get; init; }

    public string? DeviceIdentifier { get; init; }

    public string? DeviceName { get; init; }

    public string? Platform { get; init; }

    public string? AppVersion { get; init; }
}
