using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Takes a fully occupied home back out of its country's dashboard band.
/// <para>
/// <b>The only decrement in the whole projection, and it exists because Housing is the only vertical
/// whose withdrawal event carries a country code.</b> Every other counter is cumulative for exactly
/// that reason — a closed job carries a job identifier, and resolving it to a country would mean
/// querying the Jobs module, which is the reference this design forbids. Housing publishes the
/// country on occupancy, so the housing band can honestly say "available" where the others say
/// "posted".
/// </para>
/// <para>
/// Only a <em>fully</em> occupied listing decrements. A block of flats that let one of six units is
/// still somewhere a reader can live.
/// </para>
/// </summary>
public sealed class HousingOccupiedContextHandler
    : IntegrationEventHandler<HousingOccupiedIntegrationEvent>
{
    private readonly CountryContextProjector _projector;

    public HousingOccupiedContextHandler(CountryContextProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        HousingOccupiedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.IsFullyOccupied
            ? _projector.ApplyAsync(
                integrationEvent.CountryCode, CountryContextKind.Housing, -1, cancellationToken)
            : Task.CompletedTask;
    }
}
