using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Lifecycle.SuspendAccount;

/// <summary>
/// Suspends an account. Moderation action.
/// </summary>
[Auditable(Action = "identity.user.suspended")]
[Authorize(Permission = "identity:users:suspend")]
public sealed record SuspendAccountCommand : ICommand<OperationAcknowledgedResponse>
{
    public required Guid UserId { get; init; }

    public required string Reason { get; init; }

    public DateTimeOffset? SuspendedUntil { get; init; }
}
