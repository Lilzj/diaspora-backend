using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Community;

/// <summary>
/// A member followed somebody or something.
/// <para>
/// <b>A follow on a person is not recorded, and that is a deliberate boundary rather than an
/// oversight.</b> The platform has no <c>Person</c> target type: it recommends things to read, apply
/// for and attend, not people to look at. Following an author is a genuine interest signal and the
/// right way to use it would be to boost that author's content — which needs a contract carrying the
/// author's identity on each candidate, and is a future enrichment rather than something to fake
/// here.
/// </para>
/// <para>
/// A follow on a community or an organization is recorded, because both are target types this module
/// can and does recommend.
/// </para>
/// </summary>
public sealed class CommunityFollowCreatedConsumer
    : RecommendationEventConsumer<CommunityFollowCreatedIntegrationEvent>
{
    public CommunityFollowCreatedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Community;

    protected override Task HandleAsync(
        CommunityFollowCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var targetType = ContentTypeMap.Translate(integrationEvent.TargetType);

        if (targetType is null)
        {
            // A follow on a person. See the class summary.
            return Task.CompletedTask;
        }

        return RecordAsync(
            Signal(
                UserId.From(integrationEvent.FollowerId),
                SignalKind.Followed,
                integrationEvent.OccurredAt,
                targetType,
                integrationEvent.TargetId),
            cancellationToken);
    }
}
