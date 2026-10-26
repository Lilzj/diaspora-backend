using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Messaging;

namespace Diaspora.Application.Identity.EventHandlers;

/// <summary>
/// Audits a new device and, when the signal is on, says whether other accounts already use it (D-13).
/// <para>
/// <b>A count, never who.</b> One member running several accounts from one phone is how a referral or
/// a bonus is farmed, and Identity is the only module that sees devices. It reads how many other
/// accounts registered this identifier and publishes the number; Financial and Creator decide what a
/// shared device means for points and referrals. This handler runs after the commit, so it reads and
/// never writes.
/// </para>
/// </summary>
public sealed class UserDeviceRegisteredEventHandler : DomainEventHandler<UserDeviceRegisteredDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;
    private readonly IIntegrationEventPublisher _integrationEvents;
    private readonly IUserDeviceRepository _devices;
    private readonly IIdentityPolicyProvider _policies;

    public UserDeviceRegisteredEventHandler(
        IIdentityAuditWriter audit,
        IIntegrationEventPublisher integrationEvents,
        IUserDeviceRepository devices,
        IIdentityPolicyProvider policies)
    {
        _audit = audit;
        _integrationEvents = integrationEvents;
        _devices = devices;
        _policies = policies;
    }

    protected override async Task HandleAsync(
        UserDeviceRegisteredDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.DeviceRegistered,
            EntityType = IdentityAuditActions.EntityTypes.Device,
            EntityId = domainEvent.DeviceId.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string> { ["platform"] = domainEvent.Platform.ToString() }
        }, cancellationToken);

        if (!_policies.Current.EnableSharedDeviceSignal)
        {
            return;
        }

        var others = await _devices.CountOtherAccountsAsync(
            domainEvent.UserId, domainEvent.DeviceIdentifier, cancellationToken);

        if (others == 0)
        {
            return;
        }

        await _integrationEvents.PublishAsync(new AccountDeviceSharedIntegrationEvent
        {
            UserId = domainEvent.UserId.Value,
            OtherAccounts = others,
            DetectedAt = domainEvent.RegisteredAt
        }, cancellationToken);
    }
}
