using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Events;

namespace Diaspora.Modules.Recommendations.Application.EventHandlers;

/// <summary>
/// Announces that a member acted on a recommendation.
/// <para>
/// One line, because the translation itself lives in the relay — see
/// <see cref="RecommendationIntegrationEventRelay"/>. Splitting the handlers per event rather than
/// switching inside one keeps each subscription independent: a failure translating one fact cannot
/// stop another from being announced.
/// </para>
/// </summary>
public sealed class RecommendationAcceptedEventHandler : DomainEventHandler<RecommendationAcceptedDomainEvent>
{
    private readonly RecommendationIntegrationEventRelay _relay;

    public RecommendationAcceptedEventHandler(RecommendationIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        RecommendationAcceptedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
