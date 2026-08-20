using System.Diagnostics.Metrics;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Domain.Events;
using Diaspora.Modules.Providers.Observability;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// Relays a verification outcome outward, and counts it.
/// <para>
/// The most consequential event B7 publishes, and the one a consumer must not miss in the
/// <em>revoking</em> direction: a badge left on a business whose registration was withdrawn is
/// worse than never having shown one.
/// </para>
/// </summary>
public sealed class ProviderVerificationChangedEventHandler
    : DomainEventHandler<ProviderVerificationChangedDomainEvent>
{
    private readonly ProviderIntegrationEventRelay _relay;

    public ProviderVerificationChangedEventHandler(ProviderIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        ProviderVerificationChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        ProviderTelemetry.VerificationOutcomes.Add(
            1,
            new KeyValuePair<string, object?>("kind", domainEvent.Kind.ToString()),
            new KeyValuePair<string, object?>("state", domainEvent.CurrentState.ToString()));

        return _relay.PublishAsync(domainEvent, cancellationToken);
    }
}
