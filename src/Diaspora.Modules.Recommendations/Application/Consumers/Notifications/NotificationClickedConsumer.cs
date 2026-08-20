using Diaspora.Contracts.Notifications.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Notifications;

/// <summary>
/// A member followed a notification somewhere.
/// <para>
/// <b>Consumed, and nothing is sent — which is the whole of this module's relationship with
/// B13.</b> The brief is explicit that the Recommendation Platform consumes notification interaction
/// signals and does not send notifications, and the module's reference set makes that structural:
/// there is no dependency on the Notifications assembly and no way to reach its dispatcher.
/// </para>
/// <para>
/// <b>The category is what carries the information, not the notification.</b> A member who reliably
/// opens housing notifications and ignores marketplace ones has told the platform something about
/// their interests that no view count would; the notification itself is not recommendable and is
/// deliberately not recorded as a target.
/// </para>
/// </summary>
public sealed class NotificationClickedConsumer
    : RecommendationEventConsumer<NotificationClickedIntegrationEvent>
{
    public NotificationClickedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Notifications;

    protected override Task HandleAsync(
        NotificationClickedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return RecordAsync(
            Signal(
                UserId.From(integrationEvent.RecipientUserId),
                SignalKind.NotificationClicked,
                integrationEvent.ClickedAt,

                // The category, not the notification. See the class summary.
                key: integrationEvent.CategoryKey,
                keyKind: InterestKind.Category),
            cancellationToken);
    }
}
