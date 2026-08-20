using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Lifecycle.DisableAccount;

/// <summary>
/// Closes an account. Sessions end and every issued token stops working.
/// </summary>
[Auditable(Action = "identity.user.disabled")]
[Authorize(Permission = "identity:users:disable")]
public sealed record DisableAccountCommand : ICommand<OperationAcknowledgedResponse>
{
    public required Guid UserId { get; init; }

    public required string Reason { get; init; }
}
