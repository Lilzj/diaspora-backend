using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Events;

namespace Diaspora.Modules.Recommendations.Application.EventHandlers;

/// <summary>
/// Announces that observations were recorded.
/// <para>
/// One line, because the translation itself lives in the relay — see
/// <see cref="RecommendationIntegrationEventRelay"/>. Splitting the handlers per event rather than
/// switching inside one keeps each subscription independent: a failure translating one fact cannot
/// stop another from being announced.
/// </para>
/// </summary>
public sealed class SignalsRecordedEventHandler : DomainEventHandler<SignalsRecordedDomainEvent>
{
    private readonly RecommendationIntegrationEventRelay _relay;

    public SignalsRecordedEventHandler(RecommendationIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        SignalsRecordedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
