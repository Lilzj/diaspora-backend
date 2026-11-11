using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.PhoneVerification.ResendOtp;

/// <summary>
/// Requests a replacement one-time code, subject to the cooldown and resend budget.
/// </summary>
[AllowAnonymous(
    "Account recovery, reached by somebody who has lost the credential that would authenticate " +
    "them. Guarded by a one-time code and by the OTP issue budget, not by a token.")]
public sealed record ResendOtpCommand : ICommand<OtpChallengeResponse>
{
    public required string PhoneNumber { get; init; }

    public string? Purpose { get; init; }
}
