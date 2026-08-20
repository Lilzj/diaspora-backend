using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;

namespace Diaspora.Application.Identity.EventHandlers;

public sealed class UserAccountReinstatedEventHandler : DomainEventHandler<UserAccountReinstatedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;

    public UserAccountReinstatedEventHandler(IIdentityAuditWriter audit)
    {
        _audit = audit;
    }

    protected override Task HandleAsync(
        UserAccountReinstatedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.AccountReinstated,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string> { ["restoredState"] = domainEvent.RestoredState }
        }, cancellationToken);
}
