using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;

namespace Diaspora.Application.Identity.EventHandlers;

public sealed class UserAccountDisabledEventHandler : DomainEventHandler<UserAccountDisabledDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;

    public UserAccountDisabledEventHandler(IIdentityAuditWriter audit)
    {
        _audit = audit;
    }

    protected override Task HandleAsync(
        UserAccountDisabledDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.AccountDisabled,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string> { ["reason"] = domainEvent.Reason }
        }, cancellationToken);
}
