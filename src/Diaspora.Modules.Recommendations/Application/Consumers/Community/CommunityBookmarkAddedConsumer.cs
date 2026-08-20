using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Community;

/// <summary>
/// A member saved something for later.
/// <para>
/// <b>The cleanest positive signal on the platform, because it costs nothing and means something.</b>
/// A view can be an accident and an application is a whole evening; a bookmark is a deliberate act
/// with no friction, which makes it an unusually honest statement of interest.
/// </para>
/// <para>
/// <b>Community owns bookmarks and this module does not.</b> What is recorded here is a signal about
/// what the member likes; the bookmark itself lives where it belongs and is never duplicated into
/// this schema.
/// </para>
/// </summary>
public sealed class CommunityBookmarkAddedConsumer
    : RecommendationEventConsumer<CommunityBookmarkAddedIntegrationEvent>
{
    public CommunityBookmarkAddedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Community;

    protected override Task HandleAsync(
        CommunityBookmarkAddedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var targetType = ContentTypeMap.Translate(integrationEvent.ContentType);

        if (targetType is null)
        {
            // A bookmark on a post or a comment. Nothing this module can recommend.
            return Task.CompletedTask;
        }

        return RecordAsync(
            Signal(
                UserId.From(integrationEvent.UserId),
                SignalKind.Saved,
                integrationEvent.OccurredAt,
                targetType,
                integrationEvent.ContentId),
            cancellationToken);
    }
}
