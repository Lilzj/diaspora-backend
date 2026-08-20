using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Sessions.RevokeAllOtherSessions;

/// <summary>
/// Signs the member out everywhere except the device they are using.
/// </summary>
[Auditable(Action = "identity.session.revoked-all")]
[Authorize(Permission = "identity:sessions:revoke")]
public sealed record RevokeAllOtherSessionsCommand : ICommand<OperationAcknowledgedResponse>;
