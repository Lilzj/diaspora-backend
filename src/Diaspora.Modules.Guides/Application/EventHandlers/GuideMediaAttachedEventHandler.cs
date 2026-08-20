using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a media attachment.
/// <para>
/// Media is the consumer that matters: an asset referenced by a guide is an asset that must not
/// be swept as an orphan, and this is how B5 learns the reference exists. The event carries the
/// identifier and the role, never a URL — B8 stores references and Media mints links at read
/// time, against the reader (ADR-005).
/// </para>
/// <para>
/// Detachment is deliberately <em>not</em> relayed: there is no published detach contract, and
/// inventing one here would put a fifteenth event into a frozen set of fourteen. A consumer
/// learns the current reference list from the guide itself.
/// </para>
/// </summary>
public sealed class GuideMediaAttachedEventHandler
    : DomainEventHandler<GuideMediaAttachedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideMediaAttachedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideMediaAttachedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
