using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Lifecycle.ReinstateAccount;

/// <summary>
/// Lifts a suspension or hard lock, restoring the access rung the member held before it.
/// </summary>
[Auditable(Action = "identity.user.reinstated")]
[Authorize(Permission = "identity:users:reinstate")]
public sealed record ReinstateAccountCommand : ICommand<OperationAcknowledgedResponse>
{
    public required Guid UserId { get; init; }

    public string? Notes { get; init; }
}
