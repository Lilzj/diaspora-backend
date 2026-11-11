using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.PhoneVerification.VerifyPhone;

/// <summary>
/// Redeems the phone verification code.
/// <para>
/// Succeeding here promotes the member to limited access and issues their first token
/// pair in the same response. Making them sign in again immediately after proving they
/// hold the phone would be friction with no security benefit.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.phone-verified")]
[AllowAnonymous(
    "Redeeming the registration code, which is what produces the member's first token pair. There " +
    "is nothing to authenticate with yet.")]
public sealed record VerifyPhoneCommand : ICommand<AuthenticationResponse>
{
    public required string PhoneNumber { get; init; }

    public required string Code { get; init; }

    public string? DeviceIdentifier { get; init; }

    public string? DeviceName { get; init; }

    public string? Platform { get; init; }

    public string? AppVersion { get; init; }
}
