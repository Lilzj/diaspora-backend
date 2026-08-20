using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Domain.Events;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// Relays a new review outward.
/// <para>
/// The rollup is <em>not</em> recomputed here. It is recomputed synchronously in the write
/// handler, inside the same transaction, because a review that saved while the summary did not
/// is precisely the silent drift that makes a rating untrustworthy — and a handler running after
/// the commit could not put it back.
/// </para>
/// </summary>
public sealed class ProviderReviewSubmittedEventHandler
    : DomainEventHandler<ProviderReviewSubmittedDomainEvent>
{
    private readonly ProviderIntegrationEventRelay _relay;

    public ProviderReviewSubmittedEventHandler(ProviderIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ProviderReviewSubmittedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
