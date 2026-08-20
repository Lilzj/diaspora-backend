using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Passwords.ResetPassword;

/// <summary>
/// Completes account recovery: redeems the recovery code and sets a new password.
/// </summary>
[Auditable(Action = "identity.user.password-reset-completed")]
public sealed record ResetPasswordCommand : ICommand<OperationAcknowledgedResponse>
{
    public required string PhoneNumber { get; init; }

    public required string Code { get; init; }

    public required string NewPassword { get; init; }
}
