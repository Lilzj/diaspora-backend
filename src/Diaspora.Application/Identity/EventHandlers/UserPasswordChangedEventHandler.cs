using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;

namespace Diaspora.Application.Identity.EventHandlers;

public sealed class UserPasswordChangedEventHandler : DomainEventHandler<UserPasswordChangedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;

    public UserPasswordChangedEventHandler(IIdentityAuditWriter audit)
    {
        _audit = audit;
    }

    protected override Task HandleAsync(
        UserPasswordChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = domainEvent.WasReset
                ? IdentityAuditActions.PasswordResetCompleted
                : IdentityAuditActions.PasswordChanged,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.ToString(),
            SubjectUserId = domainEvent.UserId
        }, cancellationToken);
}
