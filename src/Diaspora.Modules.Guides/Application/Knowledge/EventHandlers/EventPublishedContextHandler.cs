using Diaspora.Application.Abstractions;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Counts a community event towards its country's dashboard band.
/// <para>
/// Consumes B12's published contract, never the Community Life assembly. Guarded on first
/// publication.
/// </para>
/// </summary>
public sealed class EventPublishedContextHandler
    : IntegrationEventHandler<EventPublishedIntegrationEvent>
{
    private readonly CountryContextProjector _projector;

    public EventPublishedContextHandler(CountryContextProjector projector) => _projector = projector;

    protected override Task HandleAsync(
        EventPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.IsFirstPublication
            ? _projector.ApplyAsync(
                integrationEvent.CountryCode, CountryContextKind.Events, 1, cancellationToken)
            : Task.CompletedTask;
    }
}
