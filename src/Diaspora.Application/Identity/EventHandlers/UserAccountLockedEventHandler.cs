using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.EventHandlers;

public sealed class UserAccountLockedEventHandler : DomainEventHandler<UserAccountLockedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;
    private readonly ILogger<UserAccountLockedEventHandler> _logger;

    public UserAccountLockedEventHandler(
        IIdentityAuditWriter audit,
        ILogger<UserAccountLockedEventHandler> logger)
    {
        _audit = audit;
        _logger = logger;
    }

    protected override Task HandleAsync(
        UserAccountLockedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Account {UserId} was locked: {Reason}", domainEvent.UserId, domainEvent.Reason);

        return _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.AccountLocked,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.Value.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string> { ["reason"] = domainEvent.Reason }
        }, cancellationToken);
    }
}
