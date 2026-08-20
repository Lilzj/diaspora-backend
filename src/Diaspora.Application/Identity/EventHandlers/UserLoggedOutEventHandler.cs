using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;

namespace Diaspora.Application.Identity.EventHandlers;

public sealed class UserLoggedOutEventHandler : DomainEventHandler<UserLoggedOutDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;

    public UserLoggedOutEventHandler(IIdentityAuditWriter audit)
    {
        _audit = audit;
    }

    protected override Task HandleAsync(
        UserLoggedOutDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.UserLoggedOut,
            EntityType = IdentityAuditActions.EntityTypes.Session,
            EntityId = domainEvent.SessionId.ToString(),
            SubjectUserId = domainEvent.UserId
        }, cancellationToken);
}
