using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Trust awarded a badge. Mirrors it onto the author's standing.
/// <para>
/// Badges are country-scoped, which is why the country code travels with them: "verified guide,
/// Nigeria" is a claim about knowledge of one place, and rendering it beside a guide about
/// Germany would misrepresent what Trust decided.
/// </para>
/// </summary>
public sealed class GuideAuthorBadgeAwardedEventHandler
    : IntegrationEventHandler<BadgeAwardedIntegrationEvent>
{
    private readonly GuideAuthorStandingProjector _projector;

    public GuideAuthorBadgeAwardedEventHandler(GuideAuthorStandingProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        BadgeAwardedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _projector.AwardBadgeAsync(
            integrationEvent.UserId,
            integrationEvent.Badge,
            integrationEvent.CountryCode,
            integrationEvent.AwardedAt,
            cancellationToken);
    }
}
