using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Counts a service provider towards its country's dashboard band.
/// <para>
/// <b>B7's contract carries a nullable country, and a provider without one is simply not
/// counted.</b> There is no sensible country to attribute it to, and attributing it to a wrong one
/// would be worse than a slightly low number on a band whose whole purpose is to say "there are
/// professionals here".
/// </para>
/// <para>
/// No first-publication flag exists on this contract, so a republished provider can be counted
/// twice. The counters clamp at zero and can be rebuilt from an authoritative count, which is the
/// escape hatch a projection maintained from a message bus has to have.
/// </para>
/// </summary>
public sealed class ProviderPublishedContextHandler
    : IntegrationEventHandler<ProviderPublishedIntegrationEvent>
{
    private readonly CountryContextProjector _projector;

    public ProviderPublishedContextHandler(CountryContextProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        ProviderPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _projector.ApplyAsync(
            integrationEvent.CountryCode, CountryContextKind.Providers, 1, cancellationToken);
    }
}
