using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;

namespace Diaspora.Application.Identity.EventHandlers;

public sealed class UserLoggedInEventHandler : DomainEventHandler<UserLoggedInDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;

    public UserLoggedInEventHandler(IIdentityAuditWriter audit)
    {
        _audit = audit;
    }

    protected override Task HandleAsync(
        UserLoggedInDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.UserLoggedIn,
            EntityType = IdentityAuditActions.EntityTypes.Session,
            EntityId = domainEvent.SessionId.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string> { ["accessState"] = domainEvent.AccessState }
        }, cancellationToken);
}
