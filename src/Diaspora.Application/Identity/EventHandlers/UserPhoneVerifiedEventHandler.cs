using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;

namespace Diaspora.Application.Identity.EventHandlers;

public sealed class UserPhoneVerifiedEventHandler : DomainEventHandler<UserPhoneVerifiedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;

    public UserPhoneVerifiedEventHandler(IIdentityAuditWriter audit)
    {
        _audit = audit;
    }

    protected override Task HandleAsync(
        UserPhoneVerifiedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.PhoneVerified,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.ToString(),
            SubjectUserId = domainEvent.UserId
        }, cancellationToken);
}
