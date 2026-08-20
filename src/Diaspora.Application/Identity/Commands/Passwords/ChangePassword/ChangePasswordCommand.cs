using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Passwords.ChangePassword;

/// <summary>
/// Changes the signed-in member's password.
/// </summary>
[Auditable(Action = "identity.user.password-changed")]
[Authorize(Permission = "identity:password:change")]
public sealed record ChangePasswordCommand : ICommand<OperationAcknowledgedResponse>
{
    public required string CurrentPassword { get; init; }

    public required string NewPassword { get; init; }
}
