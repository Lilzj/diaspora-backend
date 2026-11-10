using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Passwords.ResetPassword;

/// <summary>
/// Completes account recovery: redeems the recovery code and sets a new password.
/// </summary>
[Auditable(Action = "identity.user.password-reset-completed")]
[AllowAnonymous(
    "Account recovery, reached by somebody who has lost the credential that would authenticate " +
    "them. Guarded by a one-time code and by the OTP issue budget, not by a token.")]
public sealed record ResetPasswordCommand : ICommand<OperationAcknowledgedResponse>
{
    /// <summary>The address the account was registered with.</summary>
    public required string Email { get; init; }

    public required string Code { get; init; }

    public required string NewPassword { get; init; }
}
