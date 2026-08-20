using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Passwords.ForgotPassword;

/// <summary>
/// Starts account recovery by sending a one-time code to the member's phone.
/// </summary>
[Auditable(Action = "identity.user.password-reset-requested")]
public sealed record ForgotPasswordCommand : ICommand<OperationAcknowledgedResponse>
{
    public required string PhoneNumber { get; init; }
}
