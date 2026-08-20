using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Counts a marketplace listing towards its country's dashboard band.
/// <para>
/// Consumes B10's published contract, never the Marketplace assembly. Guarded on first publication.
/// </para>
/// </summary>
public sealed class ListingPublishedContextHandler
    : IntegrationEventHandler<ListingPublishedIntegrationEvent>
{
    private readonly CountryContextProjector _projector;

    public ListingPublishedContextHandler(CountryContextProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        ListingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.IsFirstPublication
            ? _projector.ApplyAsync(
                integrationEvent.CountryCode, CountryContextKind.Marketplace, 1, cancellationToken)
            : Task.CompletedTask;
    }
}
