using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Sessions.RevokeSession;

/// <summary>
/// Ends one of the caller's other sessions — the "sign out that device" action in the
/// session list.
/// </summary>
[Auditable(Action = "identity.session.revoked")]
[Authorize(Permission = "identity:sessions:revoke")]
public sealed record RevokeSessionCommand : ICommand<OperationAcknowledgedResponse>
{
    public required Guid SessionId { get; init; }
}
