using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.Intelligence.EventHandlers;

/// <summary>
/// Relays everything that happens to an official notice.
/// <para>
/// <b>One handler for the whole lifecycle, and the relay branches.</b> The alternative — a handler
/// per verb — would put the decision of which published contract a publication becomes in three
/// places, and the platform's analyzer pack fails the build on two handlers settling one domain event
/// for exactly this reason: two subscriptions to one fact eventually disagree about it.
/// </para>
/// </summary>
public sealed class OfficialNoticeChangedEventHandler
    : DomainEventHandler<OfficialNoticeChangedDomainEvent>
{
    private readonly IntelligenceIntegrationEventRelay _relay;

    public OfficialNoticeChangedEventHandler(IntelligenceIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        OfficialNoticeChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>
/// Relays a notice category being created, renamed, archived or restored.
/// <para>
/// The consequence is the same in every case: a consumer caching the vocabulary — a preference
/// screen, a routing rule, a filter chip row — has to drop it. The verb is on the event for the
/// minority that care which kind of change it was.
/// </para>
/// </summary>
public sealed class NoticeCategoryChangedEventHandler
    : DomainEventHandler<NoticeCategoryChangedDomainEvent>
{
    private readonly IntelligenceIntegrationEventRelay _relay;

    public NoticeCategoryChangedEventHandler(IntelligenceIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        NoticeCategoryChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>Relays an issuing authority being recorded, edited, verified, archived or restored.</summary>
public sealed class IssuingAuthorityChangedEventHandler
    : DomainEventHandler<IssuingAuthorityChangedDomainEvent>
{
    private readonly IntelligenceIntegrationEventRelay _relay;

    public IssuingAuthorityChangedEventHandler(IntelligenceIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        IssuingAuthorityChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>Relays a mission being recorded, edited, verified, archived or restored.</summary>
public sealed class EmbassyChangedEventHandler : DomainEventHandler<EmbassyChangedDomainEvent>
{
    private readonly IntelligenceIntegrationEventRelay _relay;

    public EmbassyChangedEventHandler(IntelligenceIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        EmbassyChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>Relays a country's emergency information changing.</summary>
public sealed class EmergencyDirectoryChangedEventHandler
    : DomainEventHandler<EmergencyDirectoryChangedDomainEvent>
{
    private readonly IntelligenceIntegrationEventRelay _relay;

    public EmergencyDirectoryChangedEventHandler(IntelligenceIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        EmergencyDirectoryChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>Relays a visa advisory being written, published, edited, archived or restored.</summary>
public sealed class VisaAdvisoryChangedEventHandler
    : DomainEventHandler<VisaAdvisoryChangedDomainEvent>
{
    private readonly IntelligenceIntegrationEventRelay _relay;

    public VisaAdvisoryChangedEventHandler(IntelligenceIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        VisaAdvisoryChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>Relays a calendar entry being published, edited, archived or restored.</summary>
public sealed class PublicHolidayChangedEventHandler
    : DomainEventHandler<PublicHolidayChangedDomainEvent>
{
    private readonly IntelligenceIntegrationEventRelay _relay;

    public PublicHolidayChangedEventHandler(IntelligenceIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        PublicHolidayChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
