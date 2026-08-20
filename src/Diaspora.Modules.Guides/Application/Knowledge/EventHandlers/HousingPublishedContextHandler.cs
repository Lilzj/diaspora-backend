using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Counts a home towards its country's dashboard band.
/// <para>
/// Consumes B11's published contract, never the Housing assembly. Guarded on first publication.
/// </para>
/// </summary>
public sealed class HousingPublishedContextHandler
    : IntegrationEventHandler<HousingPublishedIntegrationEvent>
{
    private readonly CountryContextProjector _projector;

    public HousingPublishedContextHandler(CountryContextProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        HousingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.IsFirstPublication
            ? _projector.ApplyAsync(
                integrationEvent.CountryCode, CountryContextKind.Housing, 1, cancellationToken)
            : Task.CompletedTask;
    }
}
