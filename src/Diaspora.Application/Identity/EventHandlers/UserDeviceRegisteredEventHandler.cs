using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;

namespace Diaspora.Application.Identity.EventHandlers;

public sealed class UserDeviceRegisteredEventHandler : DomainEventHandler<UserDeviceRegisteredDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;

    public UserDeviceRegisteredEventHandler(IIdentityAuditWriter audit)
    {
        _audit = audit;
    }

    protected override Task HandleAsync(
        UserDeviceRegisteredDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.DeviceRegistered,
            EntityType = IdentityAuditActions.EntityTypes.Device,
            EntityId = domainEvent.DeviceId.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string> { ["platform"] = domainEvent.Platform.ToString() }
        }, cancellationToken);
}
