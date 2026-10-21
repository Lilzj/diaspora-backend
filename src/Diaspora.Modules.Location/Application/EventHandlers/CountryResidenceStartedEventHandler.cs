using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Location.V1.Events;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Events;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Application.EventHandlers;

/// <summary>
/// Tells the rest of the platform which country a member lives in now — 21 Oct 2026.
/// <para>
/// The arrival to <see cref="CountryExperienceEndedEventHandler"/>'s departure. Relocation published
/// only the leaving, so <c>location.countryChanged</c> had nothing on the server to fire on and its
/// country condition — about where the member is now — could only ever have been answered with the
/// country they left.
/// </para>
/// </summary>
public sealed class CountryResidenceStartedEventHandler
    : DomainEventHandler<CountryResidenceStartedDomainEvent>
{
    private readonly LocationIntegrationEventRelay _relay;
    private readonly CountryNameResolver _countryNames;

    public CountryResidenceStartedEventHandler(
        LocationIntegrationEventRelay relay, CountryNameResolver countryNames)
    {
        _relay = relay;
        _countryNames = countryNames;
    }

    protected override async Task HandleAsync(
        CountryResidenceStartedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var code = CountryCode.TryCreate(domainEvent.CountryCode);

        var name = code.IsSuccess
            ? await _countryNames.NameForAsync(code.Value, cancellationToken)
            : null;

        await _relay.PublishAsync(
            new CountryResidenceStartedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                CountryCode = domainEvent.CountryCode,
                CountryName = string.IsNullOrWhiteSpace(name) ? domainEvent.CountryCode : name,
                ArrivedOn = domainEvent.ArrivedOn
            },
            cancellationToken);
    }
}
