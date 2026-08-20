using Diaspora.Contracts.Notifications.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Notifications;

/// <summary>
/// A member opened a notification.
/// <para>
/// <b>The weakest signal the platform records, and it is recorded anyway.</b> Opening a notification
/// is barely a choice — a badge is a prompt — which is why the weight is the lowest in the
/// catalogue. It earns its place because the ratio matters: a member who opens housing notifications
/// and ignores everything else has said something clear, and the only way to see the ratio is to
/// record both halves.
/// </para>
/// </summary>
public sealed class NotificationOpenedConsumer
    : RecommendationEventConsumer<NotificationOpenedIntegrationEvent>
{
    public NotificationOpenedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Notifications;

    protected override Task HandleAsync(
        NotificationOpenedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return RecordAsync(
            Signal(
                UserId.From(integrationEvent.RecipientUserId),
                SignalKind.NotificationOpened,
                integrationEvent.OpenedAt,
                key: integrationEvent.CategoryKey,
                keyKind: InterestKind.Category),
            cancellationToken);
    }
}
