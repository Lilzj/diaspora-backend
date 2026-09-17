using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Realtime;
using Diaspora.Platform.Communication;

namespace Diaspora.Modules.Notifications.Application.Consumers.Community;

/// <summary>
/// Tells every live map watching an alert's cells that the alert changed.
/// <para>
/// A <see cref="ResourceChanged"/> frame of type <c>alert</c> on each <c>geo:alerts:{cell}</c> the
/// alert's circle touches — a point touches one, a 10 km flood a handful. The frame is a signal, not a
/// copy: a client that receives one refetches its viewport, which is the only version of the map that
/// is ever right.
/// </para>
/// <para>
/// Best-effort like every broadcast on this seam. A missed frame costs one refetch interval, and a
/// member whose phone is in a pocket is not the audience — notifications are.
/// </para>
/// </summary>
public sealed class CommunityAlertRealtimeConsumer
    : IntegrationEventHandler<CommunityAlertChangedIntegrationEvent>
{
    /// <summary>The resource type every alert frame carries.</summary>
    public const string ResourceType = "alert";

    private readonly RealtimeChannelBroadcaster _broadcaster;

    public CommunityAlertRealtimeConsumer(RealtimeChannelBroadcaster broadcaster) =>
        _broadcaster = broadcaster;

    protected override async Task HandleAsync(
        CommunityAlertChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var change = new ResourceChanged(
            ResourceType,
            integrationEvent.PostId.ToString("D"),
            integrationEvent.ChangeKind,
            integrationEvent.Reason,
            integrationEvent.ChangedAt);

        var cells = GeoCells.Cover(
            integrationEvent.Latitude,
            integrationEvent.Longitude,
            integrationEvent.AffectedRadiusMetres ?? 0);

        foreach (var cell in cells)
        {
            await _broadcaster.ResourceChangedAsync(
                RealtimeChannels.ForAlertCell(cell), change, cancellationToken);
        }
    }
}
