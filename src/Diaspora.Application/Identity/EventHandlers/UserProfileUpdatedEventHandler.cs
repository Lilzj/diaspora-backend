using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;

namespace Diaspora.Application.Identity.EventHandlers;

public sealed class UserProfileUpdatedEventHandler : DomainEventHandler<UserProfileUpdatedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;

    public UserProfileUpdatedEventHandler(IIdentityAuditWriter audit)
    {
        _audit = audit;
    }

    protected override Task HandleAsync(
        UserProfileUpdatedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.ProfileUpdated,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string>
            {
                // Field names only. The values themselves are member data and have no
                // business sitting in an audit record.
                ["changedFields"] = string.Join(',', domainEvent.ChangedFields)
            }
        }, cancellationToken);
}
