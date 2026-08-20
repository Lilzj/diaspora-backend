using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Trust approved a member as a contributor — the right to publish guides.
/// <para>
/// B8 records the fact and renders it; it does not enforce publishing rights from it. The gate on
/// publishing is the access tier, which the platform's authorization pipeline applies before a
/// command runs. Two systems deciding the same thing is how one of them silently stops mattering.
/// </para>
/// </summary>
public sealed class GuideContributorApprovedEventHandler
    : IntegrationEventHandler<ContributorApprovedIntegrationEvent>
{
    private readonly GuideAuthorStandingProjector _projector;

    public GuideContributorApprovedEventHandler(GuideAuthorStandingProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        ContributorApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _projector.ApplyContributorApprovalAsync(
            integrationEvent.UserId,
            isApproved: true,
            integrationEvent.ApprovedAt,
            cancellationToken);
    }
}
