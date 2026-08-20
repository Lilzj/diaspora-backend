using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Trust recognised a member as a community guide.
/// <para>
/// A different fact from contributor approval, and worth its own field: a contributor may publish,
/// a community guide is somebody the community is being told to trust. The badge appears beside
/// their name on every guide they write.
/// </para>
/// <para>
/// Named for what it consumes rather than for Trust's <c>GuideApproved</c>, which in this module
/// would read as though a guide document had been approved. It is about a person.
/// </para>
/// </summary>
public sealed class GuideCommunityGuideApprovedEventHandler
    : IntegrationEventHandler<GuideApprovedIntegrationEvent>
{
    private readonly GuideAuthorStandingProjector _projector;

    public GuideCommunityGuideApprovedEventHandler(GuideAuthorStandingProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        GuideApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _projector.ApplyCommunityGuideApprovalAsync(
            integrationEvent.UserId,
            isCommunityGuide: true,
            integrationEvent.ApprovedAt,
            cancellationToken);
    }
}
