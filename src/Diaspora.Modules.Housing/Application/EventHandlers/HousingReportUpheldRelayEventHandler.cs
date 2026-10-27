using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Landlords;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Tells the platform a report was upheld, so Trust pays the reporter (REP-13). The report names
/// the landlord's profile; its owner is read here so Trust can refuse a self-report.
/// </summary>
public sealed class HousingReportUpheldRelayEventHandler : DomainEventHandler<HousingReportUpheldDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;
    private readonly ILandlordProfileRepository _landlords;

    public HousingReportUpheldRelayEventHandler(
        HousingIntegrationEventRelay relay, ILandlordProfileRepository landlords)
    {
        _relay = relay;
        _landlords = landlords;
    }

    protected override async Task HandleAsync(
        HousingReportUpheldDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var landlord = await _landlords.GetForReadAsync(domainEvent.LandlordId, cancellationToken);

        await _relay.PublishAsync(domainEvent, landlord?.OwnerUserId.Value, cancellationToken);
    }
}
