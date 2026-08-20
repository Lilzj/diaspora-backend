using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.PhoneVerification.ResendOtp;

/// <summary>
/// Requests a replacement one-time code, subject to the cooldown and resend budget.
/// </summary>
public sealed record ResendOtpCommand : ICommand<OtpChallengeResponse>
{
    public required string PhoneNumber { get; init; }

    public string? Purpose { get; init; }
}
