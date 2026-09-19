using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Location.V1.Events;
using Diaspora.Modules.Location.Domain.Events;

namespace Diaspora.Modules.Location.Application.EventHandlers;

/// <summary>
/// Tells the rest of the platform that a member has left a country.
/// <para>
/// <c>CountryExperienceEndedDomainEvent</c> has said since B4 that leaving is a Trust concern and
/// that Trust cannot act on a fact it is not told. This handler is the telling. It existed
/// nowhere until relocation was built, which is why a member who moved kept an open, verified
/// standing in the country they had left.
/// </para>
/// </summary>
public sealed class CountryExperienceEndedEventHandler
    : DomainEventHandler<CountryExperienceEndedDomainEvent>
{
    private readonly LocationIntegrationEventRelay _relay;

    public CountryExperienceEndedEventHandler(LocationIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        CountryExperienceEndedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CountryResidenceEndedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                CountryCode = domainEvent.CountryCode,
                DepartedOn = domainEvent.DepartedOn
            },
            cancellationToken);
    }
}
