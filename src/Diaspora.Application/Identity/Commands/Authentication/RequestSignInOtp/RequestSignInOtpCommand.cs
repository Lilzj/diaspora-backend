using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Authentication.RequestSignInOtp;

/// <summary>
/// Starts a passwordless sign-in by sending a one-time code to the member's phone.
/// Backs <c>POST /auth/otp/start</c> — the first half of the frontend's primary sign-in.
/// </summary>
public sealed record RequestSignInOtpCommand : ICommand<OperationAcknowledgedResponse>
{
    public required string PhoneNumber { get; init; }
}
