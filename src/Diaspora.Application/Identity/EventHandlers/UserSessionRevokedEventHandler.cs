using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;

namespace Diaspora.Application.Identity.EventHandlers;

public sealed class UserSessionRevokedEventHandler : DomainEventHandler<UserSessionRevokedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;

    public UserSessionRevokedEventHandler(IIdentityAuditWriter audit)
    {
        _audit = audit;
    }

    protected override Task HandleAsync(
        UserSessionRevokedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.SessionRevoked,
            EntityType = IdentityAuditActions.EntityTypes.Session,
            EntityId = domainEvent.SessionId.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string> { ["reason"] = domainEvent.Reason.ToString() }
        }, cancellationToken);
}
