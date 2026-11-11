using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Passwords.ForgotPassword;

/// <summary>
/// Starts account recovery by sending a one-time code to the member's phone.
/// </summary>
[Auditable(Action = "identity.user.password-reset-requested")]
[AllowAnonymous(
    "Account recovery, reached by somebody who has lost the credential that would authenticate " +
    "them. Guarded by a one-time code and by the OTP issue budget, not by a token.")]
public sealed record ForgotPasswordCommand : ICommand<OperationAcknowledgedResponse>
{
    /// <summary>The address the account was registered with.</summary>
    public required string Email { get; init; }
}
